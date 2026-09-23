# Testing

## In the Unity editor

- Window → General → Test Runner → EditMode → **Run All**.
- It includes **LocalMatchSmokeTest**, an automated 4-player match that takes a minute or two:
  - It presses Play in the menu scene and joins 4 virtual controllers.
  - It plays through player select, the loading screen, the opening cutscene and a skipped tutorial.
  - It drives, pauses, unplugs player 4's controller and plugs it back in.
  - It fails on any error or exception on the way.
- Leave the editor alone while it plays. Controllers you touch count as input.
- Run a single test: select it in the Test Runner → **Run Selected**.

## From the command line

- `bash tools/run-tests.sh` runs every EditMode test in a mirror copy of the project, so it works while the editor is open.
- `bash tools/run-tests.sh LocalMatchSmokeTest` runs one test class. The filter is a class or test name, or a regex.
- Results go to `Logs/test-results.xml`, and Unity's log to `Logs/test-unity.log`.
- The mirror sits at `../_doa_test_mirror/CapstoneYear4`. It takes about 7 GB and needs a short path: Windows paths over 260 characters break the import.
- `DOA_MIRROR` and `DOA_UNITY` change the mirror folder and the Unity install.
- `DOA_NO_SYNC=1` tests the mirror without copying the project into it first.
- Unity runs with graphics, because the smoke test renders real scenes. Unity started with `-nographics` crashes in URP.
