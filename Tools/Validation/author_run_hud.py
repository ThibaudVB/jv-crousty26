"""Place the run HUD in serialized Unity scenes, preserving their existing artwork.

Run from the project root. Only missing panels are placed; existing transforms are kept.
This is an offline authoring utility, never a runtime dependency.
"""
import json
import re
from pathlib import Path


def parse(text):
    matches = list(re.finditer(r"^--- !u!(\d+) &(-?\d+)\n", text, re.M))
    return {
        match[2]: (int(match[1]), text[match.start():matches[i + 1].start() if i + 1 < len(matches) else len(text)])
        for i, match in enumerate(matches)
    }


def ref(body, field):
    return re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", body, re.M)[1]


def field(body, name, value):
    return re.sub(r"^  " + name + r": .*?$", lambda _: "  " + name + ": " + value, body, flags=re.M)


def author(path):
    raw = path.read_bytes()
    newline = "\r\n" if b"\r\n" in raw else "\n"
    original = raw.decode("utf-8").replace("\r\n", "\n")
    docs = parse(original)
    hud_id = next(k for k, (_, b) in docs.items() if "Assembly-CSharp::CroustyHUD" in b)
    hud = docs[hud_id][1]

    def transform(go):
        return next(k for k, (t, b) in docs.items() if t == 224 and ref(b, "m_GameObject") == go)

    def named(name):
        return next(k for k, (t, b) in docs.items() if t == 1 and re.search(r"^  m_Name: " + re.escape(name) + r"$", b, re.M))

    def tree(root):
        transforms = {root}
        while True:
            extra = {k for k, (t, b) in docs.items() if t == 224 and ref(b, "m_Father") in transforms}
            if extra <= transforms:
                break
            transforms |= extra
        gos = {ref(docs[k][1], "m_GameObject") for k in transforms}
        return transforms | gos | {k for k, (t, b) in docs.items() if t not in (1, 224) and
            re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", b, re.M) and ref(b, "m_GameObject") in gos}

    counter = 940000000000000000

    def clone(root, exclude_gauges=False):
        nonlocal counter
        selected = tree(root)
        if exclude_gauges:
            remove = set()
            for k in selected:
                t, b = docs[k]
                if t != 224:
                    continue
                go = ref(b, "m_GameObject")
                name = re.search(r"^  m_Name: (.*)$", docs[go][1], re.M)[1]
                components = re.findall(r"component: \{fileID: (\d+)\}", docs[go][1])
                filled = any(re.search(r"^  m_Type: 3$", docs[c][1], re.M) for c in components)
                if "jauge" in name.lower() or name == "Rail vitesse" or filled:
                    remove |= tree(k)
            selected -= remove
        remap = {}
        for k in sorted(selected, key=int):
            while str(counter) in docs:
                counter += 1
            remap[k] = str(counter)
            counter += 1
        for k, new_id in remap.items():
            t, b = docs[k]
            b = re.sub(r"(?<=&)" + k + r"(?=\n)", new_id, b, count=1)
            b = re.sub(r"\{fileID: (-?\d+)\}", lambda m: "{fileID: " + remap.get(m[1], m[1]) + "}", b)
            if t == 224:
                b = re.sub(r"^  - \{fileID: (-?\d+)\}\n", lambda m: m[0] if m[1] in remap.values() else "", b, flags=re.M)
            docs[new_id] = (t, b)
        return remap

    def edit(key, name, value):
        t, b = docs[key]
        docs[key] = (t, field(b, name, value))

    def children(parent, child):
        t, b = docs[parent]
        block = re.search(r"^  m_Children:(?: \[\])?\n(?:  - \{fileID: -?\d+\}\n)*", b, re.M)
        assert block, parent
        content = block[0].replace("m_Children: []", "m_Children:")
        docs[parent] = (t, b[:block.start()] + content + "  - {fileID: " + child + "}\n" + b[block.end():])

    speed_text = ref(hud, "speedValue")
    speed_tf = transform(ref(docs[speed_text][1], "m_GameObject"))
    speed_root = ref(docs[speed_tf][1], "m_Father")
    canvas_tf = ref(docs[speed_root][1], "m_Father")
    authored = {}
    for key, name, label_name, offset, size, text in [
        ("timeValue", "Time of day", "Clock", -86, 26, "06:00 AM\n<size=52%>MORNING</size>"),
        ("timerValue", "Delivery countdown", "Countdown", -172, 28, "01:30\n<size=48%>DELIVERY RUN</size>"),
    ]:
        if re.search(r"^  " + key + r": \{fileID: [1-9]\d*\}", hud, re.M):
            authored[key] = ref(hud, key)
            continue
        remap = clone(speed_root, True)
        new_root = remap[speed_root]
        new_text = remap[speed_text]
        edit(remap[ref(docs[speed_root][1], "m_GameObject")], "m_Name", name)
        edit(remap[ref(docs[speed_text][1], "m_GameObject")], "m_Name", label_name)
        position = re.search(r"^  m_AnchoredPosition: \{x: ([^,]+), y: ([^}]+)\}", docs[new_root][1], re.M)
        edit(new_root, "m_AnchoredPosition", "{x: " + position[1] + ", y: " + str(float(position[2]) + offset) + "}")
        for k in remap.values():
            edit(k, "m_RaycastTarget", "0")
        for prop, value in {"m_fontSize": str(size), "m_fontSizeBase": str(size), "m_enableAutoSizing": "0",
            "m_HorizontalAlignment": "2", "m_VerticalAlignment": "512", "m_lineSpacing": "-4", "m_text": json.dumps(text)}.items():
            edit(new_text, prop, value)
        children(canvas_tf, new_root)
        authored[key] = new_text

    card_tf = transform(named("Carte score"))
    score_go = named("Valeur score")
    score_tf = transform(score_go)
    score_text = next(k for k, (t, b) in docs.items() if t == 114 and "TMPro.TextMeshProUGUI" in b and ref(b, "m_GameObject") == score_go)
    authored["scoreValue"] = score_text
    if re.search(r"^  scoreGain: \{fileID: [1-9]\d*\}", hud, re.M):
        authored["scoreGain"] = ref(hud, "scoreGain")
    else:
        remap = clone(score_tf)
        gain_tf, gain_text = remap[score_tf], remap[score_text]
        edit(remap[score_go], "m_Name", "Animated score gain")
        edit(gain_tf, "m_SizeDelta", "{x: 110, y: 35}")
        position = re.search(r"^  m_AnchoredPosition: \{x: ([^,]+), y: ([^}]+)\}", docs[score_tf][1], re.M)
        edit(gain_tf, "m_AnchoredPosition", "{x: " + str(float(position[1]) + 105) + ", y: " + str(float(position[2]) - 5) + "}")
        size = float(re.search(r"^  m_fontSize: ([\d.]+)", docs[score_text][1], re.M)[1])
        for prop, value in {"m_fontSize": str(max(16, size * .5)), "m_fontSizeBase": str(max(16, size * .5)),
            "m_enableAutoSizing": "0", "m_HorizontalAlignment": "2", "m_VerticalAlignment": "512",
            "m_RaycastTarget": "0", "m_text": '""', "m_fontColor": "{r: 1, g: 1, b: 1, a: 0}"}.items():
            edit(gain_text, prop, value)
        children(card_tf, gain_tf)
        authored["scoreGain"] = gain_text
    size = re.search(r"^  m_fontSize: ([\d.]+)", docs[score_text][1], re.M)[1]
    for prop, value in {"m_fontSizeMax": size, "m_fontSizeMin": "20", "m_enableAutoSizing": "1"}.items():
        edit(score_text, prop, value)
    for key, value in authored.items():
        line = "  " + key + ": {fileID: " + value + "}"
        if re.search(r"^  " + key + ":", hud, re.M):
            hud = field(hud, key, "{fileID: " + value + "}")
        else:
            hud = hud.rstrip("\n") + "\n" + line + "\n"
    docs[hud_id] = (docs[hud_id][0], hud)
    # Keep every existing document in place and append the new objects before SceneRoots.
    originals = parse(original)
    appended = "".join(b for k, (_, b) in docs.items() if k not in originals)
    result = original[:original.index("--- !u!")] + "".join(
        (appended if t == 1660057539 else "") + docs[k][1] for k, (t, _) in originals.items())
    if not any(t == 1660057539 for t, _ in originals.values()):
        result += appended
    if result != original:
        backup = Path("Temp") / ("before-authored-run-hud-" + path.name)
        if not backup.exists():
            backup.write_bytes(raw)
        path.write_bytes(result.replace("\n", newline).encode("utf-8"))
    print(path, authored, "serialized panels ready")


if __name__ == "__main__":
    for scene in ["Assets/SceneBase.unity", "Assets/Jojo/SceneJojo.unity"]:
        author(Path(scene))
