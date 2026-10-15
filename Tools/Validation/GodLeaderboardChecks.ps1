# Run with PowerShell 7. Uses the actual profile and leaderboard sources with in-memory Unity storage.
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$profile = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Menu/CroustyNameEntry.cs') -Raw
$profile = $profile.Substring($profile.IndexOf('public static class CroustyPlayerProfile'))
$leaderboard = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Menu/CroustyLeaderboard.cs') -Raw
$leaderboard = $leaderboard.Substring($leaderboard.IndexOf('public static class CroustyLeaderboardScores'))
$stubs = @"
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public static class PlayerPrefs {
  static Dictionary<string,string> data = new Dictionary<string,string>();
  public static string GetString(string key,string fallback) => data.TryGetValue(key,out var value) ? value : fallback;
  public static void SetString(string key,string value) => data[key]=value;
  public static void DeleteKey(string key) => data.Remove(key);
  public static void Save() {}
 }
 public static class Mathf {
  public static int Min(int a,int b) => Math.Min(a,b);
  public static int Max(int a,int b) => Math.Max(a,b);
 }
 public static class JsonUtility {
  static System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields=true };
  public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value,value.GetType(),options);
  public static T FromJson<T>(string value) => string.IsNullOrEmpty(value) ? default(T) : System.Text.Json.JsonSerializer.Deserialize<T>(value,options);
 }
}
"@
Add-Type -TypeDefinition ($stubs + $profile + $leaderboard)
function Assert-Check($condition, $message) {
 if (-not $condition) { throw $message }
 Write-Output "PASS: $message"
}
$key = 'ValidationOnly'
[ CroustyLeaderboardScores ]::Record(999999, 'CathyLaVodka', $key)
Assert-Check ([CroustyLeaderboardScores]::Load($key).records.Count -eq 0) 'Exact GOD name cannot submit a score'
foreach ($name in @('cathylavodka','CATHYLAVODKA','CathyLaVodka2','xCathyLaVodka','CathyLaVоdka','Alice')) {
 [UnityEngine.PlayerPrefs]::DeleteKey($key)
 [CroustyLeaderboardScores]::Record(123, $name, $key)
 $board = [CroustyLeaderboardScores]::Load($key)
 Assert-Check ($board.records.Count -eq 1 -and $board.records[0].player -ceq $name) "Other name stays eligible: $name"
 Assert-Check ([CroustyLeaderboardScores]::BestForPlayer($name,$board) -eq 123) 'Eligible personal best remains readable'
}
$legacy = '{"records":[{"id":"god","player":"CathyLaVodka","score":999999},{"id":"normal","player":"cathylavodka","score":42},{"id":"alice","player":"Alice","score":80}],"entries":[{"id":"god","player":"CathyLaVodka","score":999999}],"latestId":"god","latestPlayer":"CathyLaVodka","latestScore":999999}'
[UnityEngine.PlayerPrefs]::SetString($key,$legacy)
$board = [CroustyLeaderboardScores]::Load($key)
Assert-Check ($board.records.Count -eq 2 -and $board.entries.Count -eq 2) 'Old GOD score removed from records and podium'
Assert-Check ([CroustyLeaderboardScores]::BestForPlayer('cathylavodka',$board) -eq 42) 'Old GOD score cannot merge into differently-cased player'
Assert-Check ([CroustyLeaderboardScores]::BestForPlayer('CathyLaVodka',$board) -eq 0) 'GOD cannot borrow another players personal best'
Assert-Check ([string]::IsNullOrEmpty($board.latestPlayer) -and $board.latestScore -eq 0) 'Old GOD latest-result banner is cleared'
[CroustyLeaderboardScores]::Record(90,'Alice',$key)
Assert-Check (-not [UnityEngine.PlayerPrefs]::GetString($key,'').Contains('CathyLaVodka')) 'Next legitimate save removes old GOD data from storage'
[CroustyPlayerProfile]::Save('CathyLaVodka')
[CroustyLeaderboardScores]::Record(77,'Bob',$key)
Assert-Check ([CroustyLeaderboardScores]::BestForPlayer('Bob',$key) -eq 77) 'Exclusion checks score owner, not current menu profile'
foreach ($name in @('cathylavodka','CathyLaVodka2',' CathyLaVodka',"CathyLa`u{0001}Vodka",'Alice','')) {
 [CroustyPlayerProfile]::Save('CathyLaVodka')
 Assert-Check ([CroustyPlayerProfile]::IsGodMode) 'Exact GOD name is recognized'
 [CroustyPlayerProfile]::Save($name)
 Assert-Check (-not [CroustyPlayerProfile]::IsGodMode) "Other input revokes GOD: $name"
}
