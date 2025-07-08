## Beebyte Mono Deobfuscator

What can this tool do?

- Remove duplicated code
- Decrypt strings

Deobfuscated code can only be decompiled by RE tools like dnspy, you cannot use it to replace the original Assembly-CSharp.dll in the game folder.

### How to use it?
1. Download the latest release from the release.
2. Run the tool with the following command:
   ```bash
   .\BeeByteCleaner.exe instrument <path_to_Assembly-CSharp.dll>
   ```
3. Replace the original `Assembly-CSharp.dll` with the modified one.
4. Run the game to load as many codes as possible.
5. Close this game, `executed_methods.log` will be in the same folder as the game exe.
6. Run the tool with the following command:
   ```bash
   .\BeeByteCleaner.exe clean <path_to_original_Assembly-CSharp.dll> <path_to_executed_methods.log>
   ```
   Cleaned file will be saved as `Assembly-CSharp_clean.dll` in the same folder as the original Assembly-CSharp.dll.