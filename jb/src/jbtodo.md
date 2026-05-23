# jb/src Todo

- [ ] Define `jb/Testing` fixture folder structure: choose the scenario layout for large sample inputs, expected manifests, and expected output images under the root-level `jb/Testing` folder.
  - Impact:
    - Project progress: High - Realistic multi-GB fixtures are required before large-batch regression tests can prove import, matching, transform, and export behavior.
    - Effect on other TODOs: Unblocks - It supports Excel parsing, IO normalization, matcher evidence, manifest projection, and workbench diagnostics.
  - Industry standard:
    Data-heavy systems keep large fixture datasets outside source folders and normal Git tracking, while documenting deterministic scenario layouts that automated tests and local validation tools can consume.
  - Recommended solution:
    Use `jb/Testing` as the ignored local fixture root. Define scenario subfolders with `input`, `expected-manifest`, and `expected-output` sections, and keep any committed metadata limited to lightweight documentation or pointers to external fixture storage.
  - Answer: **FROZEN**
    - For every folder inside Testing:
      - create a folder named `foldername`+" - expected result" folder
      - fill the new folder with the expected outcome:
        - create all folders and files expected
        - use expected real information, do not use dummy information