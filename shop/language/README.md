# Russian language pack

`ocn_language_russian.ocmod.zip` is the community OpenCart 4 Russian translation, committed so that the
shop image builds reproducibly without downloading anything.

- Source: <https://github.com/ruOpenCart/opencart-language-russian-4>, release `4.1.0.4.RE8`
- sha256: `c9ac8a5b40294e3a239e0421b3a81064454a6a43e76b0149694d313f8767a1e8` (checked by `shop/Dockerfile`)
- The upstream repository has no license file. It is kept here for this demo only; check with the authors
  before using it anywhere else.
- Only the translation files are used (`catalog/` and `admin/` language directories). The pack's extension
  code, which includes a GitHub updater, is never installed.

To upgrade: download the new release asset, replace the zip, update the checksum in `shop/Dockerfile` and the
version above, then rebuild and run the tests. The pack leaves two product-page strings in English; the
Dockerfile patches them (see the `sed` there), so re-check them after an upgrade.
