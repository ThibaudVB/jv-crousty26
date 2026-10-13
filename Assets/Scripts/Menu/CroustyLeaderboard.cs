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
            ? "TERMINE UNE PARTIE POUR INSCRIRE TON SCORE !"
            : "TES MEILLEURES PARTIES · LOCAL";
    }

    private void Fill(Slot slot, int index, CroustyLeaderboardScores.Board board)
    {
        bool filled = index < board.entries.Count;
        var entry = filled ? board.entries[index] : null;
        slot.player.text = filled ? entry.player : "À TOI DE JOUER";
        slot.score.text = filled ? entry.score.ToString() : "—";
        if (slot.ribbon != null)
            slot.ribbon.sprite = filled && entry.id == board.latestId ? latestRibbon : normalRibbon;
    }
}

public static class CroustyLeaderboardScores
{
    public const string StorageKey = "Crousty.Leaderboard.v1";
    [Serializable] public sealed class Entry
    {
        public string id;
        public string player;
        public int score;
    }
    [Serializable] public sealed class Board
    {
        public List<Entry> entries = new List<Entry>();
        public string latestId;
    }

    public static Board Load(string storageKey = StorageKey)
    {
        Board board;
        try { board = JsonUtility.FromJson<Board>(PlayerPrefs.GetString(storageKey, "")); }
        catch (ArgumentException) { board = null; }
        if (board == null) board = new Board();
        if (board.entries == null) board.entries = new List<Entry>();
        board.entries.RemoveAll(e => e == null || e.score < 0 || string.IsNullOrEmpty(e.id));
        // Stable insertion order for ties: a new score does not displace an equal best.
        for (int i = 1; i < board.entries.Count; i++)
        {
            var entry = board.entries[i];
            int j = i;
            while (j > 0 && board.entries[j - 1].score < entry.score)
            { board.entries[j] = board.entries[j - 1]; j--; }
            board.entries[j] = entry;
        }
        if (board.entries.Count > 8) board.entries.RemoveRange(8, board.entries.Count - 8);
        return board;
    }

    public static void Record(int score, string player = "Toi", string storageKey = StorageKey)
    {
        var board = Load(storageKey);
        var entry = new Entry { id = Guid.NewGuid().ToString("N"),
            player = string.IsNullOrWhiteSpace(player) ? "Toi" : player.Trim(), score = Mathf.Max(0, score) };
        board.latestId = entry.id;
        int index = board.entries.FindIndex(e => e.score < entry.score);
        if (index < 0) index = board.entries.Count;
        board.entries.Insert(index, entry);
        if (board.entries.Count > 8) board.entries.RemoveRange(8, board.entries.Count - 8);
        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(board));
        PlayerPrefs.Save();
    }
}
