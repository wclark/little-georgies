# Little Georgies

Little Georgies is a dependency-free HTML/CSS/JS game for `georgist.org/little-georgies/`.

The player guides a small band of apple gatherers through a short season. Happy Georgies gather well, tired Georgies can still work but risk breaking down, and broken Georgies need the commons to recover. The theme is simple: survival gets easier when land rent is captured for the common fund instead of draining the harvest.

## Local Development

```powershell
npm run dev
```

Then open `http://localhost:5173`.

If `npm` is not on PATH, the scripts can be run directly with Node:

```powershell
node tools/dev-server.mjs
```

## Checks

```powershell
npm run check:content
npm test
npm run build
```

The build output is written to `dist/`. The playable game is copied to `dist/little-georgies/`, and a simple `dist/index.html` includes the top-level link for `georgist.org`.

## Deployment

The GitHub Actions workflow validates every push. Manual deployment syncs only the game subdirectory to `/little-georgies/` and uploads the root `index.html` that links to it:

```powershell
npm run build
aws s3 sync dist/little-georgies/ s3://YOUR_BUCKET/little-georgies/ --delete
aws s3 cp dist/index.html s3://YOUR_BUCKET/index.html
```

Configure these repository settings before running the manual deploy workflow:

- `vars.AWS_REGION`
- `vars.S3_BUCKET`
- optional `vars.CLOUDFRONT_DISTRIBUTION_ID`
- `secrets.AWS_ROLE_TO_ASSUME`

## Source Art

The included splash and character images are cropped from the reference image supplied for this project. Keep `assets/images/little-georgie-reference.png` with the repo as the source reference for future edits.
