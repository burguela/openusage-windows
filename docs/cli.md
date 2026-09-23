# Command-Line Interface

OpenUsage ships a one-shot `openusage` command for agents and scripts. It prints the documented
[`/v1/limits`](local-http-api.md#get-v1limits) JSON and exits; it never launches or leaves the menu-bar
app running. The output contains stable scalar limits and balances, not UI rows, colors, subtitles,
charts, or spend-history tiles.

```sh
openusage                 # every enabled provider, refreshing stale cache entries
openusage codex           # one provider, refreshing when its cache is stale
openusage codex --force   # refresh through the shared provider engine, cache, print, exit
```

The command and app import the same providers, authentication stores, pricing, refresh coordinator, and
snapshot cache. A normal read reuses snapshots less than five minutes old and refreshes missing or stale
ones. `--force` is the CLI equivalent of the app's manual refresh: it bypasses that freshness gate and
writes successful results to the same cache. Credentials are used locally and never appear in the output.

A provider argument names providers by plain string matching, exactly like the
[local HTTP API](local-http-api.md): an exact provider ID names that provider, and a family ID
(`claude`, `codex`) names every account card of that family — with one account that's exactly the one
card, so existing usage keeps working unchanged as multi-account support arrives. The output envelope
contains every matched provider; an ID that names nothing exits with an error. There is no aliasing
or account-picking logic.

## Install on `PATH`

In OpenUsage, open **Settings → Command Line** and click **Install…**. After the standard macOS
administrator prompt, `openusage` is available globally in new terminal sessions. The installed symlink
points to the signed helper inside OpenUsage, so in-place app updates also update the command.

## Desktop commands

The [Windows app](windows.md) (Quota Tray) drives the same binary (shipped as `quotatray-engine.exe`) with a few extra
commands. Each prints the display-ready `openusage.desktop.v1` dashboard: every provider with its
on/off state, plan, notice, links, and rows whose text is already formatted (headlines, reset
countdowns, pace notes), so a front end only lays it out.

```sh
openusage dashboard            # refresh stale providers, print the dashboard
openusage dashboard --cached   # cached values only, never touches the network
openusage dashboard --force    # refresh every enabled provider now
openusage enable cursor        # turn a provider (or a whole family) on, print the dashboard
openusage disable cursor       # turn it off
openusage meter-style used     # meters show usage used (`left` shows what's remaining)
```

The first desktop command on a new install turns on the providers whose credentials are on the machine,
like the Mac app's first launch. The dashboard uses the default layout (which metrics show, which are On
Demand, which are pinned).

Exit codes are `0` for success, `2` for invalid arguments or an unknown provider, and `4` when a
refresh or local read fails.
