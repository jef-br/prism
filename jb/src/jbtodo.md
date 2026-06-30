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
    

      


## refined excel parsing

#### Notes per dataset
- AUTOMAT2 → Excel header not found
  - The header exists on row 1, but is written in spanish rather than english.
  - The cell A1 contains the value `REFERENCIA VEEPEE` This is a synonym for FamilyID (Q: Is the synonym dictionary already implemented?)

  - This is the full *Spanish* header of the single worksheet in the xlsx:
    ```REFERENCIA VEEPEE | MODELO | CÓDIGO COLOR | Color | Descripcion | SECCIÓN | DESCRIPCIÓN | COMPOSICIÓN | CUIDADOS```
  - Literal translation to *English* =
    ```reference veepee | model | code color | color | description | section | description | composition | care```
    
- HEORAUT2 → Excel header not found
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