Rebuild public HTML after copy edits:

```
python3 deploy/site/gen.py
```

Commit the generated `index.html` files. The gateway image copies this folder; it does not run Python at build time.

Visual system: Metronic SaaSify look, static HTML only (US-17).
