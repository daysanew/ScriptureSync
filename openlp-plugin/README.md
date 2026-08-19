# ScriptureSync OpenLP plugin

This OpenLP 3.1 community plugin provides the safe bridge used by the
ScriptureSync WPF utility. It listens only on `http://127.0.0.1:4317/v1/` and
executes Bible searches and confirmed service additions sequentially on
OpenLP's Qt main thread.

OpenLP loads the plugin from:

```text
%APPDATA%\openlp\data\contrib\plugins\scripturesync
```

After installation, restart OpenLP and activate **ScriptureSync** under
**Settings > Manage Plugins**. The plugin adds one command under **Tools >
ScriptureSync Status**. It reports whether the local bridge is ready and how
many Bibles OpenLP has loaded.

Use `scripts/install-openlp-plugin.ps1` from the repository root to copy the
plugin into the current Windows user's OpenLP data folder.
