# DXCherryPick

## Plugin version

Every change to the app (`src/`) must bump `version` in `dxassistant-plugin/plugin.json` in the same PR — minor for features, patch for fixes. The DXAssistant plugin rebuilds and relaunches the app only when the plugin version changes, so without a bump users never get the change.
