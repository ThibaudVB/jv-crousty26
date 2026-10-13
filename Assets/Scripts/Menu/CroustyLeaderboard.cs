using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Updates data only. Every visual, slot and animation is authored in the menu scene.
public sealed class CroustyLeaderboard : MonoBehaviour
{
    [Serializable]
    public sealed class Slot
    {
        public TMP_Text player;
        public TMP_Text score;
        public Image ribbon;
    }
    public Slot[] podiums;
    public Slot[] rows;
    public Sprite normalRibbon;
    public Sprite latestRibbon;
    public TMP_Text status;

    private void OnEnable() => Refresh();

    public void Refresh() => Show(CroustyLeaderboardScores.Load());

    public void Show(CroustyLeaderboardScores.Board board)
    {
        for (int i = 0; i < podiums.Length; i++) Fill(podiums[i], i, board);
        for (int i = 0; i < rows.Length; i++) Fill(rows[i], i + 3, board);
        if (status != null) status.text = board.entries.Count == 0
            ? "FINISH A GAME TO RECORD YOUR SCORE!"
            : !string.IsNullOrEmpty(board.latestPlayer)
                ? $"{board.latestPlayer} : {board.latestScore} PTS · BEST {CroustyLeaderboardScores.BestForPlayer(board.latestPlayer, board)}"
                : "PERSONAL BESTS · LOCAL";
    }

    private void Fill(Slot slot, int index, CroustyLeaderboardScores.Board board)
    {
        bool filled = index < board.entries.Count;
        var entry = filled ? board.entries[index] : null;
        slot.player.text = filled ? entry.player : "YOUR TURN TO PLAY";
        slot.score.text = filled ? entry.score.ToString() : "—";
        if (slot.ribbon != null)
            slot.ribbon.sprite = filled && entry.id == board.latestId ? latestRibbon : normalRibbon;
    }
}

public static class CroustyLeaderboardScores
{
    public const string StorageKey = "Crousty.Leaderboard.v1";
    public const int VisibleRanks = 8;
    [Serializable] public sealed class Entry
    {
        public string id;
        public string player;
        public int score;
    }
    [Serializable] public sealed class Board
    {
        public List<Entry> entries = new List<Entry>();
        // Retain personal bests below rank eight so they survive subsequent sessions.
        public List<Entry> records = new List<Entry>();
        public string latestId;
        public string latestPlayer;
        public int latestScore;
    }

    public static Board Load(string storageKey = StorageKey)
    {
        Board board;
        try { board = JsonUtility.FromJson<Board>(PlayerPrefs.GetString(storageKey, "")); }
        catch (ArgumentException) { board = null; }
        if (board == null) board = new Board();
        var sources = new List<Entry>();
        if (board.records != null) sources.AddRange(board.records);
        if (board.entries != null) sources.AddRange(board.entries);
        var players = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        board.records = new List<Entry>();
        foreach (var source in sources)
        {
            if (source == null || source.score < 0 || string.IsNullOrEmpty(source.id)) continue;
            string name = NormalizePlayer(source.player);
            if (string.IsNullOrEmpty(board.latestPlayer) && source.id == board.latestId)
            { board.latestPlayer = name; board.latestScore = source.score; }
            if (players.TryGetValue(name, out var existing))
            {
                if (source.score > existing.score)
                { existing.score = source.score; existing.id = source.id; existing.player = name; }
                continue;
            }
            var record = new Entry { id = source.id, player = name, score = source.score };
            players.Add(name, record); board.records.Add(record);
        }
        SortRecords(board);
        return board;
    }

    private static void SortRecords(Board board)
    {
        // Stable insertion order for ties: a new score does not displace an equal best.
        for (int i = 1; i < board.records.Count; i++)
        {
            var entry = board.records[i];
            int j = i;
            while (j > 0 && board.records[j - 1].score < entry.score)
            { board.records[j] = board.records[j - 1]; j--; }
            board.records[j] = entry;
        }
        board.entries = board.records.GetRange(0, Mathf.Min(VisibleRanks, board.records.Count));
        var latest = string.IsNullOrEmpty(board.latestPlayer) ? null :
            board.records.Find(e => SamePlayer(e.player, board.latestPlayer));
        if (latest != null) board.latestId = latest.id;
    }

    public static string NormalizePlayer(string value)
    {
        string name = CroustyPlayerProfile.Normalize(value);
        return name.Length > 0 ? name : "You";
    }

    private static bool SamePlayer(string a, string b) =>
        string.Equals(NormalizePlayer(a), NormalizePlayer(b), StringComparison.OrdinalIgnoreCase);

    public static int BestForPlayer(string player, string storageKey = StorageKey) =>
        BestForPlayer(player, Load(storageKey));

    public static int BestForPlayer(string player, Board board)
    {
        var records = board.records != null && board.records.Count > 0 ? board.records : board.entries;
        var record = records.Find(e => SamePlayer(e.player, player));
        return record != null ? record.score : 0;
    }

    public static void Record(int score, string player = "You", string storageKey = StorageKey)
    {
        var board = Load(storageKey);
        string name = NormalizePlayer(player);
        score = Mathf.Max(0, score);
        var entry = board.records.Find(e => SamePlayer(e.player, name));
        if (entry == null)
        {
            entry = new Entry { id = Guid.NewGuid().ToString("N"), player = name, score = score };
            board.records.Add(entry);
        }
        else if (score > entry.score) entry.score = score;
        entry.player = name;
        board.latestPlayer = name;
        board.latestScore = score;
        board.latestId = entry.id;
        SortRecords(board);
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(board));
        PlayerPrefs.Save();
    }
}
