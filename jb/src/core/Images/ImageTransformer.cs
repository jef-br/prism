namespace Prism.Core;

/*
 * Handles all image transformations
 * 
 * Specific transformations are loaded using a strategy design pattern
 * Every specific transformation is written out in a separate .cs class and their names all start with the prefix "Tx_"
 * Receives images coming from PreProcessor.cs
 * Sends images back to Pipeline.cs
*/
