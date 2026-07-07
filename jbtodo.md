Refine the web workbench layout:
- Should be a bit less "beige"
- Should have a "darkmode" theme
- Format doesn't allow a compact and complete review mechanism for matching and transforming
- Too much scrolling needed to see relevant information causing drowned download zip link
- Upscaling currently not explicitly mentioned is a good thing.
- No real feedback during the import & export stage. Hard to know whether a job is blocked or not.


-----

Optimize the pipelining architecture so that Import and Match are fused togther to remove double image I/O.
Keep in mind that we want to try and keep the matchingservice open to the public as well.

-----

- The CiMini-golden-review wasn't perfect. following is actual vs desired:
    ## CURRENT
    -----
    OK  2021_3024_46_A.jpg                             90861025  det8  [NumericMatcher.Bracket1]
    OK  2021_3024_46_B.jpg                             90861025  det9  [NumericMatcher.Bracket1]
    OK  2021_3041_65_A.jpg                             90861026  det8  [NumericMatcher.Bracket1]
    OK  23211008_02_A.jpg                              90861083  det8  [NumericMatcher.Bracket1]
    OK  23211008_02_B.jpg                              90861083  det9  [NumericMatcher.Bracket1]
    OK  23231096_35_A.jpg                              90861071  det8  [NumericMatcher.Bracket1]
    OK  24211507_CARDIGAN_76_MAGENTA_B.jpg             90861052  det9  [NumericMatcher.Bracket1]
    OK  24211511_86_A.jpg                              90861075  det8  [NumericMatcher.Bracket2]
    OK  24211511_96_A.jpg                              90861076  det8  [NumericMatcher.Bracket2]
    OK  CARDIGAN_MAGENTA76_A.jpg                       90861052  det10  [SiblingPropagator]
    OK  CARDIGAN_MAGENTA76_DETAIL.jpg                  90861052  det8  [SiblingPropagator]
    OK  Pareo Exotica.jpg                              94613033  det8  [StringMatcher.Bracket3]
    OK  Pareo_exotica_F1.jpg                           94613033  det9  [StringMatcher.Bracket3]
    OK  Pareo_exotica_F2.jpg                           94613033  det10  [StringMatcher.Bracket3]

    ## DESIRED
    -----
    OK  2021_3024_46_A.jpg                             90861025  det0   [NumericMatcher.Bracket1]
    OK  2021_3024_46_B.jpg                             90861025  det1   [NumericMatcher.Bracket1]
    OK  2021_3041_65_A.jpg                             90861026  det0   [NumericMatcher.Bracket1]
    OK  23211008_02_A.jpg                              90861083  det0   [NumericMatcher.Bracket1]
    OK  23211008_02_B.jpg                              90861083  det1   [NumericMatcher.Bracket1]
    OK  23231096_35_A.jpg                              90861071  det0   [NumericMatcher.Bracket1]
    OK  24211507_CARDIGAN_76_MAGENTA_B.jpg             90861052  det1   [NumericMatcher.Bracket1]
    OK  24211511_86_A.jpg                              90861075  det0   [NumericMatcher.Bracket2]
    OK  24211511_96_A.jpg                              90861076  det0   [NumericMatcher.Bracket2]
    OK  CARDIGAN_MAGENTA76_A.jpg                       90861052  det0   [SiblingPropagator]
    OK  CARDIGAN_MAGENTA76_DETAIL.jpg                  90861052  det2   [SiblingPropagator]
    OK  Pareo Exotica.jpg                              94613033  det2   [StringMatcher.Bracket3]
    OK  Pareo_exotica_F1.jpg                           94613033  det0   [StringMatcher.Bracket3]
    OK  Pareo_exotica_F2.jpg                           94613033  det1   [StringMatcher.Bracket3]



- Re: MEPAL4-gold-data
  - Current mapping is 100% perfect
  - Ideally: after mapping empty columns are dropped to shrink search space / model size