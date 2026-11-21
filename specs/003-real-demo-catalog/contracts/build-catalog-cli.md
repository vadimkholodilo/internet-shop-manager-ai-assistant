# Contract: build-catalog command line

```bash
dotnet run tools/build-catalog/build-catalog.cs -- [--limit N] [--dumps LIST] [--out DIR] [--cache DIR] [--archive]
```

| Option | Default | Meaning |
|--------|---------|---------|
| `--limit N` | none | Select N products spread over subjects and brands (research R6). Without it, every product that passes the filters. |
| `--dumps LIST` | `12` | Dump files to read: numbers and ranges, e.g. `12` or `9-12` (R5). |
| `--out DIR` | `out/catalog` | Writes `DIR/catalog.json` and `DIR/images/`. Existing files there are replaced. |
| `--cache DIR` | `.cache/build-catalog` | Cards, photos and the host map (R7). |
| `--archive` | off | Also writes `DIR/catalog.tar.gz` with the file and images (R10). |

Run from the repository root. Output path names are relative to it.

**Output on success**: a summary on stdout: products written, per-subject and per-brand counts, out-of-stock count,
skipped products by reason, requests made vs. served from cache, output size.

**Exit codes**: `0` success; `1` output failed validation or the `--limit` rules could not be met (nothing written);
`2` invalid arguments; `3` the dump could not be read.

**Updating the committed subset**:

```bash
dotnet run tools/build-catalog/build-catalog.cs -- --limit 100 --out out/subset
cp out/subset/catalog.json data/sample-catalog.json && rm -rf data/images && cp -r out/subset/images data/images
```
