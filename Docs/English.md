# English game text

All player-facing text is English in the title menu, name entry, credits,
leaderboard, loading panel, pause menu, results, delivery objectives, GPS,
police alerts, flight state and passenger counter. Serialized scene text and
runtime defaults were both translated. Bonus cards use their English labels.
The manager's French title image is replaced by editable `MANAGER` text using
the existing RetroBlendy font. Compact scores use an English decimal separator
(`6.8k`). Player-entered names remain unchanged.

`EnglishChecks` passed in Unity 6000.4.0f1:
`ENGLISH_CHECKS_DONE failures=0`. Checks cover all authored text in the title
and gameplay scenes, the older game scenes, the loading prefab and runtime
name prompts, leaderboard, objectives, GPS and manager title. The name-entry
screen and HUD were rendered and visually inspected. The known editor Search
indexing exception and the two historical menu texture messages are counted
separately from game errors.

![English HUD](Previews/hud-english.png)
![English name entry](Previews/name-english.png)
