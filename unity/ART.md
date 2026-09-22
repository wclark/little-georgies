# Prototype Art

## Staged Opening Art (2026-09-13)

Built-in image generation was used for these two assets. The original full village remains a style reference but is no longer the runtime backdrop.

- `Assets/Resources/Art/OrchardField.png`: structure-free orchard and meadow, used at every stage.
- `Assets/Resources/Art/BuildingSheet.png`: separate weaving-bench and hut sprites; displayed only after their corresponding unlock or construction.
- `Assets/Resources/Art/GeorgieIcon.png`: unchanged copy of the existing `assets/images/georgie-happy.png` portrait, not a newly generated character.
- `Icons/LittleGeorgie.ico`: the same portrait's PNG bytes in a Windows ICO container, created by `tools/Install-DesktopShortcut.ps1` without altering image pixels.

### Orchard Edit Prompt

Use case: precise-object-edit. Asset: Unity 2D game background. Edit target: this painted village image. Preserve its 1672x941 wide framing, elevated camera, exact apple orchard position on the left, large open grassy lower/middle clearing, distant lake, sky and surrounding trees. REMOVE EVERY man-made object: all houses/cottages, the center market/store building, right workshop, all baskets, crates, barrels, bench, fences, stone walls, signposts and roads. Replace their footprints with natural open meadow and grass in the same painted gouache style. This is the game's first stage before anyone has built anything: only wild apple trees and open field, bare ground patches and a shade tree where a single character can sleep outdoors. No people, no characters, no animals, no buildings, no tents, no campfire, no beds, no equipment, no text, no UI. Keep all former building sites as broad flat traversable meadow for future unlockable building sprites. Do not add structures anywhere including the distance. Keep composition and scale very close to original.

### Building Sprite Prompt

Use case: stylized-concept. New game sprite asset sheet, using the attached painted village ONLY as style and elevated camera reference. Canvas 1536x1024, exactly TWO equally sized columns. LEFT column: a very simple open-air basket-weaving workplace, a rough low log workbench with bundles of willow reeds, a small stool, and one half-woven basket; NO roof, NO hut, NO market stall, NO sign. RIGHT column: exactly ONE tiny primitive single-room timber hut with a straw thatched roof, a dark open doorway, no chimney, no fence, no landscaping. Both objects fully visible, same elevated three-quarter camera, painterly gouache detail and clear edges matching the reference. Both centered in their own 768x1024 cells, bottom baseline at 90% canvas height, generous margin, each sized to occupy roughly 75% cell width. EMPTY BACKGROUND must be perfectly flat pure RGB #FF00FF magenta, including the spaces between and around objects, no checkerboard and no transparency. No ground patch, no grass, no shadows outside object silhouettes, no people, no words, no letters, no UI or border. Do not copy the reference landscape: only the two isolated sprites on magenta.


Generated for this project from the supplied Little Georgie visual direction. The user's character reference is retained in the web project's `assets/images/georgie-happy-scene.png`. No downloaded third-party art packs are included.

- `Assets/Resources/Art/Village.png`: new painted village background, 1672 x 941.
- `Assets/Resources/Art/GeorgieSheet.png`: reference-derived eight-frame sheet, 1536 x 1024. Four walk frames, idle, work, tired, and rest.
- `Assets/Resources/Art/GeorgieSprite.shader`: removes the uniform magenta background at runtime. The delivered sheet is RGB, not an alpha cutout.
- The original illustrated cards are references, not sprite rigs. Role-specific bodies, richer production animation, carrying props, and additional structures remain future art work.

## Background Prompt

Use case: illustration-story. Asset type: full-bleed painted background for a Unity 2D village simulation, landscape 16:9, 1536x864 or similar. An inviting small apple-growing settlement seen in a gently elevated three-quarter 2D picture-book view. Hand-painted gouache, crisp charming game art, soft afternoon light, saturated greens with red apples, pale turquoise distant sky, slate rooftops and natural timber. Large broad open grassy walking space across the lower half and middle; a few subtle footpaths join distinct sites. Composition for game coordinates: orchard with three apple trees on the far left around x=18%, y=42%; small open basket-making workshop on the right around x=80%, y=45%; shared apple store with crates in center at x=50%, y=50%; three small cottages with blue-gray roofs along upper middle around x=52%, y=25%; shaded resting patch under a tree lower-left around x=20%, y=75%. Leave clear flat ground in front of every site and a clear broad walking corridor connecting them. Entire scene fills image to edges, horizon only top 10%. No people, no characters, no animals, no words, no labels, no UI, no frame, no vignette. Buildings small relative to landscape. This is an actual playable background not a promotional illustration.

## Character Prompt

Use case: stylized-concept, identity-preserve. Reference input is the original Little Georgie character, preserve recognizable large brown eyes, oversized shaggy gray hair and full beard, round nose, small squat body, green leaf loincloth, bare feet, friendly humble gatherer. Generate a production Unity 2D animation sprite sheet on a genuinely TRANSPARENT alpha background. Canvas exactly a 4 columns by 2 rows equal-cell grid, 1536x1024 landscape. EACH of eight cells contains exactly one identical full-body Little Georgie from head to feet, centered horizontally, no props, no floor or shadows, fully inside its cell with generous 10% padding, all same scale and feet baseline. Painterly gouache 2D game illustration with clear contours, no plastic 3D. Face and body consistently turned 20 degrees toward the right, three-quarter side view. Top row four WALK CYCLE frames in order: left foot forward/right back, feet passing beneath body, right foot forward/left back, feet passing beneath body. Bottom row: frame1 standing idle happy; frame2 arm raised picking apple (no apple); frame3 tired drooping shoulders and half shut eyes standing; frame4 sitting and resting. Consistent head position/scale across top row, aligned cells. No words, no captions, no frames or dividing lines, no card borders, no scene. Do not include the original orchard or any reference text. Exactly 8 separate characters evenly arranged in 4 columns and 2 rows, transparent empty space.

## Background Correction Prompt

The first character-sheet generation had an opaque checkerboard instead of true transparency. This edit replaced it with a uniform chroma key without manually editing the bitmap.

Precise edit target: the provided eight-frame 4x2 Little Georgie sprite sheet. Change ONLY the checkerboard background. Replace all checkerboard in every cell with perfectly flat uniform solid RGB #FF00FF pure magenta for game chroma key. NO transparency and NO checkerboard. Keep all eight characters, their detailed illustrated colors, placement, size, poses and exact 4-column 2-row grid unchanged. Do not add shadows. Full original 1536x1024 canvas. Preserve each character fully within its original cell. A pure solid #FF00FF background everywhere outside the characters is essential.
