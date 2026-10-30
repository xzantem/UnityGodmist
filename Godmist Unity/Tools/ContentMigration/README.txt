Frozen release identities — v0.1.0

Source: tag v0.1.0, commit 89159e69fca45e96dc56a3bf5ff0c7de9ba066a0.
The JSON is the immutable historical list. Do not regenerate it from current assets.
The generator requires Python 3, PyYAML and the release tag in Git; it writes this JSON
and Scripts/Systems/SaveLoad/LegacyContentIdentities.Generated.cs. It reads Unity YAML,
never edits it. New migrations should retain these aliases.

StableContentIdentitySetup applies identities and binds condition references using
Unity Editor API. Re-running it validates initialized assets without reassigning IDs.
Do not run broad content setup tools to apply this migration.
