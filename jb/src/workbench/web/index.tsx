/*
This file is the main page for our workbench. It is a single page application.

**The workbench serves to visualise the entire pipeline and every intermediate stage.**
**By using the workbench, a user can better understand the pipeline logic and suggest improvements**

 * The web stack is Next.js.
 * if a visual element will appear multiple times, make a component for it.
    * Good example:
        * image thumbnail
        * an image preview grid with zoom control slider that holds image thumbnail components
        * image preview window
        * FamilyID-Images
        * The image grid might use the image thumbnail component
        * ...
    * every component is added to a separate file
 * There should only be 2 css files:
    * One containing nothing but colors and fonts. 
    * The other containing all css classes.
    * Keep classes neat and tiny

* it should have a drag&drop upload field to signal the user what to do but drag&drop should work anywhere on the page.
* The workbench needs to be able to upload multiple excel files, images and zip files to the api.
* A scrollbar should never hide another scrollbar.
* Only page elements with actual data/content in them show scrollbars.

* Functionality is grouped per section in `jb\src\workbench\web\sections`
* Components are located in `jb\src\workbench\web\components`
* 

*/