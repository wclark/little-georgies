# Little Georgies

Little Georgies is a dependency-free HTML/CSS/JS game for `georgist.org/little-georgies/`.

The player starts with one happy Little Georgie and guides the settlement for as many days as it can survive. Each day, every Georgie can work or rest. Workers gather, lead, farm, or build first; then each Georgie eats an apple if the pantry has one. A fed worker becomes tired, a fed rester becomes happy, and an unfed Georgie becomes broken.

The display evolves with the rules:

- Solo stage: one large mood image, tutorial copy, one work/rest choice, and only the current apple count.
- Named band stage: the first Georgie becomes Henry, new Little Georgies are named from a small name set, and growth depends on happy-turn percentage plus aggregate apples gathered.
- Specialist village stage: Henry becomes Chief Henry in the main focus slot, with Farmer Georgies and Builder Georgies as clickable group tiles. Group views summarize aggregate mood and production, and individual tiles drill down to each Georgie's details.

The Chief can levy and redistribute apples, the Farmer gathers more with baskets, and the Builder makes baskets and houses. Group images use the median mood of the Georgies inside that group.

## Local Development

```powershell
npm run dev
```

Then open `http://localhost:5173`.

Add `?debug` to the URL, such as `http://localhost:5173/?debug`, to show the current phase, thresholds, rates, resources, plans, and Georgie state in the side ledger.

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
aws s3 sync dist/little-georgies/ s3://georgist.org/little-georgies/ --delete --region us-west-1
aws s3 cp dist/index.html s3://georgist.org/index.html --region us-west-1
```

Then invalidate CloudFront distribution `E1QMD3NCCUN9RC` for `/`, `/index.html`, and `/little-georgies/*`.

Configure this repository setting before running the manual deploy workflow:

- `secrets.AWS_ROLE_TO_ASSUME`

## Source Art

The included splash and character images are cropped from the reference images supplied for this project. The solo-stage scene images are full mood crops from `assets/images/little-georgie-reference.png`, which is kept in the repo as source reference for future edits but excluded from the deployed build.
