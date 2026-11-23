# RealistikOsu.Performance

pp for RealistikOsu, calculated with the [RealistikOsu/osu](https://github.com/RealistikOsu/osu) fork, so relax pp is the same code the lazer rulesets run.

It replaces the Rust `performance-service` and keeps its environment variables and its `/api/v1/calculate` wire format, so the score servers calling it don't change.

## Components

Pick one with `APP_COMPONENT` (or as the first argument).

| Component | What it does |
| --- | --- |
| `api` | `GET /api/v1/status` and `POST /api/v1/calculate`, the old request and response bodies. |
| `queue` | Reads scores from the Redis list `osu-queue:performance`, calculates their pp from the stored hit counts, writes it back and updates the player's totals and leaderboards. |
| `deploy` | Recalculates every score of the modes and relax bits asked for, then every player. |
| `lazer_deploy` | The same for osu!lazer scores. |
| `recalc` | One player, every mode and variant. |
| `recalc_zero` | Scores still on 0pp, then only their players. |

### Queue items

Push JSON onto the list with `LPUSH osu-queue:performance`. The worker reads the score row, so nothing else is needed.

```json
{"table": "scores_relax", "score_id": 123456}
```

`table` is one of `scores`, `scores_relax`, `scores_ap` or `lazer_scores`. A failed item is retried three times.

### Difficulty cache

Difficulty attributes are the costly part of a calculation. They are cached per map file checksum, ruleset, calculator version and the mods that change difficulty, in memory and in Redis (`performance:difficulty:*`), so several instances share them. A change to the osu fork bumps the calculator version and starts a fresh cache.

## Configuration

| Variable | Default | |
| --- | --- | --- |
| `APP_COMPONENT` | | |
| `API_HOST`, `API_PORT` | `127.0.0.1`, `8080` | `api` only |
| `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_DATABASE` | | |
| `REDIS_HOST`, `REDIS_PORT` | | |
| `BEATMAPS_PATH` | `.` | Where `.osu` files are kept. |
| `OSU_BEATMAP_BASE_URL` | `https://old.ppy.sh/osu` | Where missing ones are fetched from. |
| `STATSD_HOST`, `STATSD_PORT` | empty, `8125` | Metrics stay off while the host is empty. |
| `PERFORMANCE_QUEUE` | `performance` | |
| `DIFFICULTY_CACHE_ENTRIES`, `DIFFICULTY_CACHE_TTL_HOURS` | `20000`, `168` | |

## Building

The osu fork is pinned in `osu.ref` and fetched into `osu/`.

```sh
scripts/fetch-osu.sh
dotnet test
docker build -t realistikosu/performance .
```

To work on the fork at the same time, make `osu` a symlink to your checkout and `fetch-osu.sh` leaves it alone.
