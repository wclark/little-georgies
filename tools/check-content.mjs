import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(process.cwd());
const requiredFiles = [
  "index.html",
  "site/index.html",
  "src/main.js",
  "src/game.js",
  "src/styles.css",
  "assets/images/splash-orchard.png",
  "assets/images/georgie-happy.png",
  "assets/images/georgie-tired.png",
  "assets/images/georgie-broken.png",
  "assets/images/apple.png"
];

for (const file of requiredFiles) {
  if (!existsSync(resolve(root, file))) {
    throw new Error(`Missing required file: ${file}`);
  }
}

const index = readFileSync(resolve(root, "index.html"), "utf8");
const main = readFileSync(resolve(root, "src/main.js"), "utf8");
const styles = readFileSync(resolve(root, "src/styles.css"), "utf8");
const rootPage = readFileSync(resolve(root, "site/index.html"), "utf8");

for (const asset of [
  "./assets/images/splash-orchard.png",
  "./assets/images/georgie-happy.png",
  "./assets/images/georgie-tired.png",
  "./assets/images/georgie-broken.png"
]) {
  if (!index.includes(asset) && !main.includes(asset)) {
    throw new Error(`The app does not reference ${asset}`);
  }
}

if (!rootPage.includes("/little-georgies/")) {
  throw new Error("site/index.html must include a top-level /little-georgies/ link");
}

if (/letter-spacing:\s*-[0-9.]/.test(styles)) {
  throw new Error("Negative letter spacing is not allowed");
}

console.log("Content checks passed");
