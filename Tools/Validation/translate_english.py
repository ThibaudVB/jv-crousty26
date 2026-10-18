"""Translate player-facing Unity strings without changing object names or IDs."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TRANSLATIONS = {
    "OBJECTIF": "TASK", "LIVRAISONS": "DELIVERIES", "RETOUR": "BACK",
    "MENU PRINCIPAL": "MAIN MENU", "REPRENDRE": "RESUME",
    "ÉCHAP  •  REPRENDRE": "ESC  •  RESUME", "LA VILLE T'ATTEND": "THE CITY IS WAITING",
    "RÉGLAGES": "SETTINGS", "CRÉDITS": "CREDITS", "VIDÉO": "VIDEO",
    "COMMANDES": "CONTROLS", "CONTRÔLEUR": "CONTROLLER", "Contrôleur": "Controller",
    "CLAVIER / SOURIS": "KEYBOARD / MOUSE", "SOURIS": "MOUSE",
    "Sensibilité": "Sensitivity", "Volume général": "Master volume", "Plein écran": "Fullscreen",
    "ACTIVÉ": "ON", "DÉSACTIVÉ": "OFF", "JOUER": "START", "QUITTER": "QUIT",
    "GÉRANT": "MANAGER", "Gaz Toxique": "TOXIC GAS", "RIZ": "RICE", "VOL": "FLIGHT",
    "Bonus": "Power-ups", "Delivery": "Deliveries", "Toi": "You",
    "Merci d'avoir pris le bus !": "Thanks for riding with us!",
    "Récupérer les commandes au restaurant": "Pick up your orders at the restaurant",
    "Récupère les commandes au\nrestaurant de Crousty (0/1)": "Pick up your orders at\nCrousty's restaurant (0/1)",
    "Livre les commandes aux clients\n({0}/{1})": "Deliver your orders\n({0}/{1})",
    "Tournée terminée ! Retourne\nau restaurant de Crousty.": "Run complete! Return to\nCrousty's restaurant.",
    "Tournée échouée ! Retourne au restaurant.": "Run failed! Return to the restaurant.",
    "Hop hop hop ! Les commandes\nattendent, on se bouge !": "Come on! The orders are\nwaiting. Let's get moving!",
    "Encore {0} commandes !\nLes clients ont faim !": "{0} orders to go!\nThe customers are hungry!",
    "Plus qu'une commande,\nne lâche rien !": "Just one order left.\nKeep going!",
    "Dépêche-toi, le temps file !": "Hurry up! Time is running out!",
    "Bien joué ! {0} points !\nRetourne au restaurant !": "Nice work! {0} points!\nHead back to the restaurant!",
    "Les clients attendent encore…\nRetourne au restaurant !": "Customers are still waiting...\nHead back to the restaurant!",
    "ENTRE TON NOM POUR PRENDRE LE VOLANT": "ENTER YOUR NAME TO GET BEHIND THE WHEEL",
    "PRÊT À FAIRE CRACHER LE MOTEUR ?": "READY TO FIRE UP THE ENGINE?",
    "ENTRÉE POUR JOUER": "PRESS ENTER TO START", "ON PART !": "LET'S GO!",
    "SCENEBASE DOIT ÊTRE AJOUTÉE AU BUILD": "THE GAME LEVEL IS UNAVAILABLE",
    "RETOUR AU MENU": "BACK TO MENU", "OUPS ! RETOUR AU MENU": "OOPS! BACK TO MENU",
    "C'EST CHAUD, ON Y VA !": "HOT AND READY. LET'S GO!",
    "Niveau indisponible": "Level unavailable", "La ville n'a pas pu être préparée": "The city could not be loaded",
    "TERMINE UNE PARTIE POUR INSCRIRE TON SCORE !": "FINISH A GAME TO RECORD YOUR SCORE!",
    "À TOI DE JOUER": "", "MEILLEUR SCORE PAR JOUEUR · LOCAL": "PERSONAL BESTS · LOCAL",
    "BUS FIGHTER - CHUTE": "BUS FIGHTER - FREE FALL", "Passagers : ": "Passengers: ",
    "GPS : carte indisponible": "GPS: map unavailable", "GPS : meshes non lisibles": "GPS: map data unavailable",
    "GPS : navigation indisponible": "GPS: navigation unavailable", "GPS : prends des commandes": "GPS: pick up some orders",
    "GPS : aucune route accessible": "GPS: no route available", "GPS : rejoins une route": "GPS: get back on the road",
    "[E] GPS : prochaine livraison": "[E] GPS: next delivery", "GPS epuise": "GPS depleted",
    "POLICE ! Le bus a ete repere": "POLICE! You've been spotted!",
    "VOITURE DE POLICE DETRUITE ! ALERTE ": "POLICE CAR DESTROYED! WANTED LEVEL ",
    " - RENFORTS !": " - BACKUP INCOMING!", "ALERTE ": "WANTED LEVEL ",
    " - La police appelle des renforts !": " - Police are calling for backup!",
    "Police semee !": "You lost the police!", "Nuage PET : la police est aveuglee !": "TOXIC GAS: the police can't see!",
    " - POURSUITE POLICIERE": " - POLICE CHASE", " - Hors de vue... ": " - Out of sight... ",
}

def write(path, text):
    path.write_text(text, encoding="utf-8", newline="")

changed = []
for folder in (ROOT / "Assets/Scripts", ROOT / "Assets/Jojo/Scripts"):
    for path in folder.rglob("*.cs"):
        before = path.read_text(encoding="utf-8-sig")
        after = before
        for source, target in TRANSLATIONS.items():
            # Match a whole literal or a literal's visible fragment, leaving IDs alone.
            source_cs = source.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")
            target_cs = target.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")
            after = after.replace('"' + source_cs + '"', '"' + target_cs + '"')
        if path.name == "CroustyHUD.cs":
            after = after.replace('GetCultureInfo("fr-FR")', 'InvariantCulture')
        if path.name == "CroustyLeaderboard.cs":
            after = after.replace(' PTS · RECORD ', ' PTS · BEST ')
        if path.name == "SceneBaseSession.cs":
            after = after.replace('Passagers : {displayedCount}', 'Passengers: {displayedCount}')
        if path.name == "BusGPS.cs":
            after = after.replace('GPS : {distance:0} m', 'GPS: {distance:0} m')
        if after != before:
            write(path, after)
            changed.append(str(path.relative_to(ROOT)))

fields = r"m_text|m_Text|referencePickup|referenceDeliver|referenceComplete|referenceFailed|speakerName|startLine|ordersLeftLine|lastOrderLine|hurryLine|runCompleteLine|failedLine|crashLine|englishName|frenchName"
scalar = re.compile(r"(?ms)^( +)(" + fields + r"): ([^\n]*(?:\n(?!\1\S)[^\n]*)*)")
for path in list((ROOT / "Assets").rglob("*.unity")) + list((ROOT / "Assets").rglob("*.prefab")):
    if any(part in {"TextMesh Pro", "Mirza Beig", "_Recovery"} for part in path.parts):
        continue
    before = path.read_text(encoding="utf-8-sig")
    def replace_scalar(match):
        raw = match.group(3)
        value = raw.strip()
        if value.startswith('"'):
            try:
                value = json.loads(re.sub(r"\\x([0-9a-fA-F]{2})", r"\\u00\1", value))
            except ValueError:
                return match.group(0)
        elif value.startswith("'") and value.endswith("'"):
            value = value[1:-1].replace("''", "'")
        target = TRANSLATIONS.get(value)
        if target is None:
            folded = re.sub(r"\s+", " ", value).strip()
            target = next((translation for original, translation in TRANSLATIONS.items() if re.sub(r"\s+", " ", original).strip() == folded), None)
        if target is None:
            return match.group(0)
        # Keep newline(s) between fields. JSON quoted strings are also valid YAML.
        suffix = re.search(r"\s*$", raw).group(0)
        return match.group(1) + match.group(2) + ": " + json.dumps(target, ensure_ascii=True) + suffix
    after = scalar.sub(replace_scalar, before)
    # The loading screen's final message is a serialized array entry.
    after = after.replace("- C'EST CHAUD, ON Y VA !", "- HOT AND READY. LET'S GO!")
    # Existing bonus language fields otherwise overwrite the translated labels at runtime.
    after = re.sub(r"(?m)^(  language:) 1$", r"\1 0", after)
    if after != before:
        write(path, after)
        changed.append(str(path.relative_to(ROOT)))

print("Translated files:\n" + "\n".join(changed))
