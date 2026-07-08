namespace Prism.Lib.Ingress;

/*
Contains all logic needed to extract tokens from any input image's filename
- numerical tokens
- alphabetical tokens

Tokenization happens aggressively:
    - separators (_, -, space, camelCase, digits)
    - numeric groups
    - directional substrings
    - compound words as well as stems of words (if stemmed, the token value is lowered)

Tokenization refers to the whole image collection to scan for recurring patterns indicating an ordering scheme
    * signal strength for ordering schemes = consistency_across_sets_of_images * predictiveness_of_order * uniqueness

    * Numeric monotonicity to measure
        strictly-increasing/decreasing tokens
        absence of gaps per image set
        duplicates

    * Lexical ordering using dictionary
        * does lexicographic sort produce stable sequences?
    
    * Semantic tokens (any token appearing in `DetOrderRules.json` and `ImageNGP.json` raise in value)
        * if tokens match any of the values in those files, their value is raised used SemanticalRelevanceWeight as a multiplier

    * Separate identity tokens from ordering tokens
        * identity tokens:
            * High cardinality tokens = high value for matching, low value for ordering
            * very inconsistent or unique appearance accross entire image collection
            * weak positional correlation
        * ordering tokens:
            * low-medium cardinality
            * reused globally
            * strong position correlation
            * often suffix/prefix clustered

    

*/
