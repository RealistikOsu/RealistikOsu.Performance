#!/bin/sh
# Puts the osu fork at the commit in osu.ref into ./osu. OSU_REFERENCE can point at a local clone to take objects from.
set -eu

cd "$(dirname "$0")/.."

read -r url sha < osu.ref

if [ -L osu ]; then
    echo "osu is a symlink, leaving it alone"
    exit 0
fi

if [ ! -d osu/.git ]; then
    git init -q osu
    git -C osu remote add origin "$url"
fi

if [ -n "${OSU_REFERENCE:-}" ]; then
    echo "$OSU_REFERENCE/.git/objects" > osu/.git/objects/info/alternates
fi

if [ "$(git -C osu rev-parse HEAD 2>/dev/null || true)" != "$sha" ]; then
    git -C osu fetch -q --depth 1 origin "$sha"
    git -C osu checkout -q -f --detach "$sha"
fi
