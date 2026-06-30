# Dataset test feedback

- [ ] HEORAUT2: No images directly in the dataset. They are all inside zip files.
- 
-----
    
- [ ] HEROAUT3 has the same failure as AUTOMAT2 although it really shouldn't have.
- These are the column headers:
    ```Family ID Veepee | EAN / BARCODE | Reference-colour | COLOUR CODE | NGP | Genre | Catégorie | Désignation marketing / Nom de la collection | Type de produit | Composition | Conseils d'entretien | Coupe (courte, longue, midi) ```
  - Literal translation to *English* =
    ```Family id veepee | EAN / BARCODE | reference color | color code | NGP | genre | category | marketing name / name of the collection | product type | washing instructions | cut (short, long, mid)```
    
- It is very clear that the cell in A1 ("Family id veepee") should have been picked up as the FamilyID column header.
  - There are enough "known" column headers to confirm this is indeed the row containing the column headers (familyID, EAN, color, NGP, product type, washing instructions)
  - splitting the cell by space " " then checking for keywords could resolve the issue? Fuzzy matching might be better.

-----

- INPUTMA24 should have been renamable by collating and deduplicating the worksheets. Conclusion has to be that target behavior isn't correctly implemented yet.
  - 3 exel files:
    - Header rows are:
      - for "PYORDEN20 - DESC Y NORMATIVAS.xlsx": 2 worksheets:
        - Sheet 1 → Family ID | Label | EAN | RefCo | MARCA | DESCRIPCIÓN | COLOR | COMPOSICIÓN/MATERIALES | DIMENSIONES (SI APLICA) | Tipo de tapa | Formato | Rayada | Sin Cierre | EDAD RECOMENDADA
        - Sheet 2 → Family ID | Label | EAN | RefCo | OPERADOR ECONOMICO RESPONSABLE
      
      - for "PYORDEN20 - MODIFY DATA.xlsx": |FamilyId | REFERENCIA | Operador económico responsable |
      
      - for "matched_PYORDEN20 - MODIFY DATA.xlsx": | REFERENCIA | Operador económico responsable |
    
    - This should have been resulted in the following collated and deduplicated worksheet
    ```FamilyID | Label | EAN | REFERENCIA | RefCo | Operador económico responsable  | MARCA | DESCRIPCIÓN | COLOR | COMPOSICIÓN/MATERIALES | DIMENSIONES (SI APLICA) | Tipo de tapa | Formato | Rayada | Sin Cierre | EDAD RECOMENDADA```
    
    This translates into:
    
    ```FamilyID | Label | EAN | reference | RefCo | Operador económico responsable  | brand | description | color | materials | dimensions | Tipo de tapa | format | Rayada | Sin Cierre | EDAD RECOMENDADA```
      - all columns with the same column header should have collapsed into one column
      - "exotic" column headers (such as 'Operador económico responsable', 'Sin Cierre', and 'EDAD RECOMENDADA') are not found in the dictionary and kept as-is because the cells in thems columnses might be useful during bracket 3 and 4.
      - Family ID and FamilyId have resolved to one 'FamilyID' column
    
-----

- INPUTMA27
  - This is the header column I found on row 9:
```
SKU PACK/COMP/SOLO | "FAMILYID  PACK /COMP /SOLO " | FAMILY ID LOT |  | Marque | Société | Type de lot BOOST | Référence FNR | EAN UC | EAN PCB | EAN UTILISE | Désignation marque | Color code | Color label | Désignation site VP | Région / Pays | Appellation | Degré alcool | Couleur | Contenance | Millésime | Décompo | Code op reprise | "Family Id reprise" | "Code OP reprise PREVIEW" | "FamilyId reprise PREVIEW" | Poids net (kg ou L) | PCB | Type de lot | UVC | "- Produit majoritairement recyclable  (si les produits recyclés issus du processus de recyclage représentent plus de 50 % de la masse des déchets collectés)  - Entièrement recyclable  (si >95%)  - Vide  (si <50%)" | "- Produit contenant au moins X% de matière recyclée  - Vide  (si pas d'information disponible) " | "- Packaging majoritairement recyclable  (si les produits recyclés issus du processus de recyclage représentent plus de 50 % de la masse des déchets collectés)  - Entièrement recyclable  (si >95%)  - Vide  (si <50%)" | "- Emballage contenant au moins X% de matière recyclée  - Vide  (si pas d'information disponible) " | "- Contient une substance dangereuse: nom des substances  - Contient une substance extrêmement préoccupante: nom des substances  - Vide  (si non concerné)" | "Dans le cadre de la modulation eco-contributions, cet emballage a fait l'objet d'une:  - d'une pénalité en raison de ...  - d'un bonus grâce à ...   - Vide  (si non concerné)" | Pays | Pays | Pays | Pays | Pays | Pays | "- Contient au moins [X milligrammes] de métaux précieux” ou ''contient au moins [X milligrammes] de (préciser l'élément)  (si >1miligrammes)  - Vide  (si <1miligrammes)" | "- Contient au moins [X milligrammes] de terres rares” ou ''contient au moins (X milligrammes) (préciser l'élément)  (si >1miligrammes)  - Vide  (si <1miligrammes)" | De 0,0 à 10 | De 0,0 à 10 | Opérateur économique responsable de la sécurité du produit (fournisseur, importateur, mandataire... :nom, adresse postale, email) | Avertissements de sécurité | Désignation produit | Description détaillée produit | Coloris/motif | Composition | conseils d'entretien | Longueur en cm | Largeur en cm | Hauteur en cm | Poids en Kg | Contenance | Longueur en mm | Largeur en mm | Hauteur en mm |  Poids en g |  |  |  |  |  |  |  |  | 

```
  - It is written in French and contains conflicting information.
  - However, it DOES list several FamilyID columns.
    - These columns should have been deduplicated.
      - Rule: if multiple familyID columns exist → check cells. If cells have identical values, merge and consider this column +1 more important than other familyID columns.
      - In a familyID row header cell, there might be a modifier keyword "new" → +1 more important.
      - in the header row, if a column header cell contains 'opcode' and 'id' exists, it is considered a FamilyID columnheader cell.

-----

- MMERO26
  - 2 excel files found:
    1. "MMERO26 - IMAGES MATCH.xlsx" → Header = ```ean | Family id | ID | Collectie | Subcollection | Code | Naam | CompositionFull | Color | Model_f | Model_n | TitleFr | TitleNl | DescriptionFr | DescriptionNl | Description1Fr | Description1Nl```  
    2. "MMERO26_27-02-2026-10-49-56.xlsx" → Header = ```Photos | Référence | Famille | Désignation | Nom de l’image (sans extension) | Chemin de l'image | Statut | Validation | EANS```
  
  - On purpose, this dataset contains many duplicate images, and many poorly named images that can't be matched.
  - However: The images inside `MMERO26\images\25W\800 X 1200\` such as `25W_308_D.jpg ` can be renamed by linking the familyID column to the image via one of the other columns. The cells in column 'Chemin de l'image' happen to match to files inside that folder as well as the `300 X 450` folder. "/25W_385_60_D.jpg" matches identically to 2 images. The images should already be deduplicated by this point so only the image inside `800 x 1200` should remain.
  - Improve the matching algorithm by generalizing this case so similar cases can be handled. -> filename found in the excel? -> match to familyID. (This is basically Bracket 1 of matching but to another column. This should be carefully implemented)

-----

- SPACINI29
  - well known case
    - images are present twice
    - one excel file
    - easy renaming case
    - reports as 50% OK which is confusing -> This is actually 100% OK. 86 images had a duplicate that was removed prior to matching -> 86 images to match... 86 images matched. = 100% OK
-----

- TinyTest
  - 100% OK as expected
  - Smallest known easy proven matchable dataset

-----


## refined excel parsing

- when collacting worksheets, copy the cell VALUES-ONLY. No formulas, no layout, only the values.
- include translation (spec'd below)
- check worksheet collation happens
- check column deduplication happens

#### Notes per dataset
- AUTOMAT2 → Excel header not found
  - The header exists on row 1, but is written in spanish rather than english.
  - The cell A1 contains the value `REFERENCIA VEEPEE` This is a synonym for FamilyID (Q: Is the synonym dictionary already implemented?)

  - This is the full *Spanish* header of the single worksheet in the xlsx:
    ```REFERENCIA VEEPEE | MODELO | CÓDIGO COLOR | Color | Descripcion | SECCIÓN | DESCRIPCIÓN | COMPOSICIÓN | CUIDADOS```
  - Literal translation to *English* =
    ```reference veepee | model | code color | color | description | section | description | composition | care```
    
- HEORAUT2 + INPUTMA25 → Excel header not found
  - This is correct. No column here qualifies as a FamilyID column. Good job for KO'ing this.

- HEROAUT3 → Excel header not found
  - The header is written in french. Even though many terms are transparent, the current code doesn't handle this properly.
  - Cell A1 actually contains "Family ID" + " Veepee" and yet it didn't get recognized.
  
- INPUTMA23
  - The column header row is on row 17.
  - This is that full header row: ```NO | BRAND | PRODUCT CATEGORY | PRODUCT TYPE | GENDER | EAN AW2025 | Family ID | CODE AW2025 | STYLE | STYLE & COLOR | COLOR | SEASON | Veepee 7th April 2026 "3 Italian Designer Friends - Mia Tomazzi, Lia Biassoni & MelinaC" | URL images | Special Prices Veepee April sale 1 | 88% and more discount products: max volume push needed to keep the prices in 2026! | NEW Discounted Wholesale EUR (from November 2024) | NEW Retail EUR (from February 2024 | DISCOUNT TO RETAIL | WEIGHT | Lenght in cm | Height in cm | Width in cm | MADE IN | TYPE | MATERIAL | Style Name | Leather Look | How to wear | Type of bag | size | Style | HANDLES | SHOULDER STRAP | CLOSURE | INTERIOR | DECORATION AND OTHERS |  | Product image #1 | Product image #2 | Product image #3 | Product image #4 | Model image```
    - Note that the header row contains these columns: `EAN AW2025`, `PRODUCT TYPE`, `Family ID`, `COLOR`, `MATERIAL` which should have been identified and used

- INPUTMA24

#### Generalized direction for Excel parsing

*Note to Claude Code: This is the user's handwritten plan. It is a "best effort" plan. Before implementing, revise this section checking for logical inconsistencies and possible further optimalisations.*




##### Column header detection

- Column header can be written in one of many languages: DE,ES,EN,FR,IT,NL.
- The HeaderRowIndicators are currently detected using a custom dictionary located in `jb\src\core\config\ExcelConfig.json` . These do not list all translations. This needs to be solved.
  - There is a dictionary that might be usable `jb\src\core\config\TranslationDictionary.json` to solve this.
- All the header row terms I have found during this test are listed in this `.md` file.
    - These need to be translated in all languages mentioned above.
- Target behavior:
  - `jb\src\core\config\TranslationDictionary.json` stores all words/terms/translations using the english key as primary key
  - `jb\src\core\config\ExcelConfig.json` uses keys from the TranslationDictionary.json to list the HeaderRowIndicators instead of just keeping a list of words.

- Family ID Column identification:
  - Once the column header is found, if that column name signals it might be the FamilyID column -> verify **all** cells in that column have exactly one 8 digit number and all values are unique. If true, This is definitive confirmation that this is our FamilyID column.

    - If cells contain a 10 digit sequence + '-' or '_' followed by 2 more digits. Example: 1234123412-01 and 1234567890_01 --> the header is probably "refco"
      
- if "modelo" and "codigo color" (or synonyms) column headers are present, and the cells match these 2 patterns, a refco column can be auto-generated by combining both columns if such a column doesn't already exist.
  - the "color" column contains the color as a string. Here, the values are in Spanish. The colors should be translated to English via the ModelBuilder (using a strategy translation class)
  - "code color" can also be written as "colorcode" (if in english). It holds a 2 digit number that is the last part of a refco number
        - if "modelo" contains a 10 digit number, it holds the first 10 digits of a refco number.
  - AUTOMAT2 contained a "Descripcion" and a "DESCRIPCIÓN" column. Both columns translate to "description".
    - if A and B are identical merge by keeping only one cell
    - if A and B are non-identical
      - if numerical: combine the values of both cells separated by an underscore
      - if non-numerical, merge the contents of both cells into one cell.
  - "composition" translates into material or fabric
  - CUIDADOS ("care") translates into washing instructions

##### Job acceptance criteria
  - **Current**: Jobs with zips and xlsx but no images are rejected before PRISM peeks inside the zip to check for images.
  - **Target behavior**: PRISM peeks inside the zip files to read their central directory before rejecting a jobrequest.
    - If the central directory of the lists images and/or excels these count towards the jobrequest acceptance criteria.
    - If the central directory of a zip contains only files that do not qualify (= are not images as per prism_config.json, .zip, or .xlsx files) the zip is dropped without processing it and added to the manifest as a non-qualifying zip.
  - Important: The KO is correct. Even after translating and implementing target behavior no column here qualifies as a FamilyID column. Good job for KO'ing this.