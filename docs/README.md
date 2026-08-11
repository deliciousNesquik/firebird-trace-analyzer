# Documentation site

The user-facing documentation for **Firebird Trace Analyzer**, built with
[Astro](https://astro.build) + [Starlight](https://starlight.astro.build) and published to
GitHub Pages at <https://deliciousnesquik.github.io/firebird-trace-analyzer/>.

## Working on the docs

> Requires **Node.js 22.12+** (Astro 7).

```bash
cd docs
npm install
npm run dev      # local preview at http://localhost:4321/firebird-trace-analyzer/
npm run build    # production build into docs/dist
npm run preview  # preview the production build
```

## Structure

- `src/content/docs/` — the pages (Markdown / MDX). The sidebar is configured in `astro.config.mjs`.
- `public/media/` — screenshots and GIFs referenced by the guides. **Drop your media here**; the
  guides reference files such as `media/load-local.gif`, `media/load-ssh.gif`,
  `media/explore-filter.gif` and `media/reports.gif`.
- `astro.config.mjs` — site title, sidebar, `site`/`base` (GitHub Pages path).

## Deployment

Pushing to `master` with changes under `docs/**` (or running the **Docs** workflow manually)
builds the site and deploys it to GitHub Pages via `.github/workflows/docs.yml`.

One-time setup: in the repository, go to **Settings → Pages → Source** and select
**GitHub Actions**.
