# Little Georgies

Little Georgies is a dependency-free HTML/CSS/JS game for `georgist.org/little-georgies/`.

The player starts with one happy Little Georgie and guides the settlement through a short season. Each day, every Georgie can work or rest. Workers gather, lead, farm, or build first; then each Georgie eats an apple if the pantry has one. A fed worker becomes tired, a fed rester becomes happy, and an unfed Georgie becomes broken.

The display evolves with the rules:

- Solo stage: one Little Georgie, tutorial copy, and only the current apple count.
- Named band stage: the first Georgie becomes Henry, new Little Georgies are named from a small name set, and growth depends on happy-turn percentage plus aggregate apples gathered.
- Specialist village stage: named individuals become anonymous Chief, Farmer, and Builder groups tracked by role and mood counts. Baskets, houses, and the common fund appear only here.

The Chief can levy and redistribute apples, the Farmer gathers more with baskets, and the Builder makes baskets and houses.

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

The included splash and character images are cropped from the reference images supplied for this project. `assets/images/little-georgie-reference.png` is kept in the repo as source reference for future edits, but it is excluded from the deployed build.
