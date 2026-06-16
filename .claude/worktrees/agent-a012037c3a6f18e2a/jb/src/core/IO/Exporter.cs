/*
responsible for exporting the renamed image collection

Can be exported as:
    A. zip file that contains all the renamed images + a manifest.json
    B. json object literal containing:
        - manifest
        - images.ok[] and images.ko[] journey entries for frontend visualization
        - optional originalImages only when explicitly requested
*/
