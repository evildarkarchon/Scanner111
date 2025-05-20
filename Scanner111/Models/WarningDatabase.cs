namespace Scanner111.Models;

public class WarningDatabase
{
        /// <summary>
    /// Contains warning messages and error texts for the application
    /// </summary>
    public static class WarningsDatabase
    {
        #region Game Warnings
        
        public const string WarnRootPath = 
            "❌ CAUTION : YOUR GAME FILES ARE INSTALLED INSIDE OF THE DEFAULT PROGRAM FILES FOLDER!\n" +
            "  Having the game installed here may cause Windows UAC to prevent some mods from working correctly.\n" +
            "  To ensure that everything works, move your Game or entire Steam folder outside of Program Files.\n" +
            "-----";

        public const string WarnDocsPath =
            "❌ CAUTION : MICROSOFT ONEDRIVE IS OVERRIDING YOUR DOCUMENTS FOLDER PATH!\n" +
            "  This can sometimes cause various save file and file permissions problems.\n" +
            "  To avoid this, disable Documents folder backup in your OneDrive settings.\n" +
            "-----";
        #endregion

        #region Wrye Bash Warnings
        
        public const string WarnCorrupted =
            "❓ Wrye Bash could not read these plugins, there's a high chance they are corrupted.\n" +
            "  Resave them in Creation Kit and run Plugin Checker again to see if anything changed.\n" +
            "  If corruption persists, consider completely removing these plugins and their mod files.\n" +
            "  -----";

        public const string WarnIncorrectEslFlag =
            "❓ These plugins have an incorrectly assigned ESL flag or extension.\n" +
            "  To fix, remove the ESL flag with xEdit or rename the extension to .esp.\n" +
            "  They can frequently cause the game to crash if you don't fix these plugins.\n" +
            "  -----";

        public const string WarnMissingMasters =
            "❓ These plugins have missing requirements (required mods cannot be found).\n" +
            "  Either install all missing requirements or completely remove these plugins.\n" +
            "  Plugins with missing requirements won't work correctly and can crash the game.\n" +
            "  -----";

        public const string WarnDelinquentMasters =
            "❓ These plugins are not in the correct load order. You should run Wrye Bash\n" +
            "  and reorder plugins with orange checkboxes until they turn green or yellow.\n" +
            "  Incorrect load order will either crash the game or break some ingame items.\n" +
            "  -----";

        public const string WarnOldHeaderFormVersions =
            "❓ These plugins have a header that is older than the valid Creation Kit version.\n" +
            "  Such plugins need to be resaved in Creation Kit to fix the incorrect header.\n" +
            "  -----";

        public const string WarnDeletedNavmeshes =
            "❓ These plugins have deleted navmeshes. These can often cause a crash\n" +
            "  in specific areas. Try to find a patch that fixes their navmeshes\n" +
            "  or disable these plugins first if you ever get a navmesh crash.\n" +
            "  -----";

        public const string WarnDeletedBaseRecords =
            "❓ These plugins have deleted base records. These might cause a crash\n" +
            "  and deleted records can only be manually restored with xEdit.\n" +
            "  -----";

        public const string WarnHitmEs =
            "❓ These plugins contain Higher Index Than Master-list Entries, which are mainly\n" +
            "  caused by improper xEdit or CK edits. Resave these plugins with Creation Kit.\n" +
            "  If HITMEs persist, such plugins may not work correctly and can crash the game.\n" +
            "  -----";

        public const string WarnDuplicateFormIDs =
            "❓ These Form IDs occur at least twice in the listed plugins. This is undefined behavior\n" +
            "  that may result in crashes or unpredictable issues and this can only be fixed with xEdit.\n" +
            "  Contact the mod authors and consider uninstalling these plugins if you encounter problems.\n" +
            "  -----";

        public const string WarnRecordTypeCollisions =
            "❓ These Records are overriding each other, but have different record types. This behavior\n" +
            "  can often lead to crashes or cause various issues and this can only be fixed with xEdit.\n" +
            "  Contact the mod authors and consider uninstalling these plugins if you encounter problems.\n" +
            "  -----";

        public const string WarnProbableInjectedCollisions =
            "❓ These Injected Records are overriding each other, but have different Editor IDs.\n" +
            "  This can cause some problems and their Editor IDs should be renamed to match each other.\n" +
            "  Contact the mod authors and consider uninstalling these plugins if you encounter problems.\n" +
            "  -----";

        public const string WarnInvalid =
            "❓ These plugins were made with a non-standard or invalid Creation Kit version.\n" +
            "  Resave these plugins in Creation Kit and check if problems or errors persist.\n" +
            "  -----";

        public const string WarnCleaningWith =
            "❓ These plugins contain ITMs and/or UDRs which should be cleaned manually with\n" +
            "  Quick Auto Clean (QAC) or automatically with Plugin Auto Cleaning Tool (PACT).\n" +
            "  -----";
        #endregion

        #region Mod Scan Warnings

        public const string WarnModsReminders =
            "=================== MOD FILES SCAN ===================\n" +
            "-- REMINDERS --\n" +
            "❓ (-FORMAT-) -> Any files with an incorrect file format will not work.\n" +
            "  Mod authors should convert these files to their proper game format.\n" +
            "  If possible, notify the original mod authors about these problems.\n\n" +
            "❓ (-PREVIS-) -> Any mods that contain custom precombine/previs files\n" +
            "  should load after the PRP.esp plugin from Previs Repair Pack (PRP).\n" +
            "  Otherwise, see if there is a PRP patch available for these mods.\n\n" +
            "❓ (ANIMDATA) -> Any mods that have their own custom Animation File Data\n" +
            "  may rarely cause an *Animation Corruption Crash*. For further details,\n" +
            "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n" +
            "❓ (DDS-DIMS) -> Any mods that have texture files with incorrect dimensions\n" +
            "  are very likely to cause a *Texture (DDS) Crash*. For further details,\n" +
            "  read the *How To Read Crash Logs.pdf* included with the CLASSIC exe.\n\n" +
            "❓ (XSE-COPY) -> Any mods with copies of original Script Extender files\n" +
            "  may cause script related problems or crashes. For further details,\n" +
            "  read your AUTOSCAN report files after scanning your crash logs.\n" +
            "-----";

        public const string WarnModsPathInvalid =
            "❌ ERROR : YOUR MODS FOLDER PATH IS INVALID! PLEASE OPEN SETTINGS\n" +
            "AND ENTER A VALID FOLDER PATH FOR *MODS Folder Path* FROM YOUR MOD MANAGER.\n" +
            "-----";

        public const string WarnModsPathMissing =
            "❌ MODS FOLDER PATH NOT PROVIDED! TO SCAN ALL YOUR MOD FILES, PLEASE OPEN\n" +
            "SETTINGS AND ENTER A FOLDER PATH FOR *MODS Folder Path*.\n" +
            "-----";

        public const string WarnModsBsArchMissing =
            "❌ BSARCH EXECUTABLE CANNOT BE FOUND. TO SCAN ALL YOUR MOD ARCHIVES, PLEASE DOWNLOAD\n" +
            "THE LATEST VERSION OF BSARCH AND EXTRACT ITS EXE INTO THE SCANNER111 DATA FOLDER\n" +
            "BSArch Link: https://www.nexusmods.com/newvegas/mods/64745?tab=files\n" +
            "-----";

        public const string WarnModsPluginLimit =
            "# [!] CAUTION : ONE OF YOUR PLUGINS HAS THE [FF] PLUGIN INDEX VALUE #\n" +
            "* THIS MEANS YOU ALMOST CERTAINLY WENT OVER THE GAME PLUGIN LIMIT! *\n" +
            "Disable some of your esm/esp plugins and re-run the Crash Logs Scan.\n" +
            "-----";
        #endregion

        #region Crashgen Warnings

        public const string WarnTomlAchievements =
            "❌ CAUTION : Achievements Mod and/or Unlimited Survival Mode is installed, but Achievements parameter is set to TRUE #\n" +
            "FIX: Open *Buffout4.toml* and change Achievements parameter to FALSE, this prevents conflicts with Buffout 4.\n" +
            "-----";

        public const string WarnTomlMemory =
            "❌ CAUTION : Baka ScrapHeap is installed, but MemoryManager parameter is set to TRUE\n" +
            "FIX: Open *Buffout4.toml* and change MemoryManager parameter to FALSE, this prevents conflicts with Buffout 4.\n" +
            "-----";

        public const string WarnTomlf4Ee =
            "❌ CAUTION : Looks Menu is installed, but F4EE parameter under [Compatibility] is set to FALSE\n" +
            "FIX: Open *Buffout4.toml* and change F4EE parameter to TRUE, this prevents bugs and crashes from Looks Menu.\n" +
            "-----";

        public const string WarnOutdated =
            "# [!] CAUTION : YOUR BUFFOUT 4 VERSION MIGHT BE OUT OF DATE, UPDATE BO4 IF NECESSARY #\n" +
            "  Original Buffout Version: https://www.nexusmods.com/fallout4/mods/47359\n" +
            "  Buffout VR / NG Version: https://www.nexusmods.com/fallout4/mods/64880\n" +
            "  Buffout 4 Guide: https://www.nexusmods.com/fallout4/articles/3115";

        public const string WarnMissing =
            "# [!] CAUTION : SOME BUFFOUT 4 FILES MIGHT BE MISSING OR HAVE BEEN INCORRECTLY INSTALLED #\n" +
            "  Original Buffout Version: https://www.nexusmods.com/fallout4/mods/47359\n" +
            "  Buffout VR / NG Version: https://www.nexusmods.com/fallout4/mods/64880\n" +
            "  Buffout 4 Guide: https://www.nexusmods.com/fallout4/articles/3115";

        public const string WarnNoPlugins =
            "* [!] NOTICE : BUFFOUT 4 WAS NOT ABLE TO LOAD THE PLUGIN LIST FOR THIS CRASH LOG! *\n" +
            "  Scanner111 cannot perform the full scan. Provide or scan a different crash log\n" +
            "  OR copy-paste your *loadorder.txt* into your main Scanner111 folder.";
        #endregion

        #region XSE Warnings

        public const string WarnXseOutdated =
            "[!] CAUTION : YOUR F4SE VERSION MIGHT BE OUT OF DATE, UPDATE F4SE IF NECESSARY\n" +
            "  FALLOUT 4 SCRIPT EXTENDER (F4SE): (Download Latest Build) https://f4se.silverlock.org\n" +
            "  Extract all files inside *f4se_0_06_XX* folder into your Fallout 4 root game folder.\n" +
            "-----";

        public const string WarnXseMissing =
            "[!] CAUTION : SOME SCRIPT EXTENDER FILES MIGHT BE MISSING OR HAVE BEEN INCORRECTLY INSTALLED\n" +
            "  You should reinstall F4SE, manual installation without a mod manager is highly advised.\n" +
            "  FALLOUT 4 SCRIPT EXTENDER (F4SE): (Download Latest Build) https://f4se.silverlock.org\n" +
            "  Extract all files inside *f4se_0_06_XX* folder into your Fallout 4 root game folder.\n" +
            "-----";

        public const string WarnXseMismatch =
            "[!] CAUTION : SOME SCRIPT EXTENDER FILES MIGHT BE BROKEN OR OTHER MODS ARE OVERRDING THEM\n" +
            "  Reinstall F4SE or use 'Check Mod Files' to see if any installed mods contain F4SE scripts.\n" +
            "  FALLOUT 4 SCRIPT EXTENDER (F4SE): (Download Latest Build) https://f4se.silverlock.org\n" +
            "  Extract all files inside *f4se_0_06_XX* folder into your Fallout 4 root game folder.\n" +
            "-----";
        #endregion

        #region Interface Messages
        
        public const string StartMessage =
            "PRESS THE *SCAN CRASH LOGS* BUTTON TO SCAN ALL AVAILABLE CRASH LOGS\n\n" +
            "PRESS THE *SCAN GAME FILES* BUTTON TO SCAN YOUR GAME & MOD FILES\n\n" +
            "IF YOU ARE USING MOD ORGANIZER 2, RUN SCANNER111 WITH THE MO2 SHORTCUT\n" +
            "READ THE DOCUMENTATION FOR MORE DETAILS AND INSTRUCTIONS";

        public const string HelpPopupMain =
            "PRESS THE *SCAN CRASH LOGS* BUTTON TO SCAN ALL AVAILABLE CRASH LOGS\n" +
            "PRESS THE *SCAN GAME FILES* BUTTON TO SCAN YOUR GAME & MOD FILES\n\n" +
            "IF YOU ARE USING MOD ORGANIZER 2, RUN SCANNER111 WITH THE MO2 SHORTCUT\n" +
            "READ THE DOCUMENTATION FOR MORE DETAILS AND INSTRUCTIONS\n\n" +
            "CUSTOM SCAN FOLDER\n" +
            "> You can set your custom scan folder that contains your crash log files.\n" +
            "  Scanner111 will already scan crash logs from your Documents folder by default.\n\n" +
            "STAGING MODS FOLDER\n" +
            "> To scan all mod files, select your staging mods folder from your mod manager.\n" +
            "  This is the folder where your mod manager keeps copies of all extracted mod files.\n\n" +
            "If you have trouble running this program or wish to submit your crash log files\n" +
            "for help from our support team, join the Collective Modding Discord server.";

        public const string HelpPopupBackup =
            "BACKUP > Backup files from the game folder into the Scanner111 Backup folder.\n" +
            "RESTORE > Restore file backup from the Scanner111 Backup folder into the game folder.\n" +
            "REMOVE > Remove files only from the game folder without removing existing backups.\n\n" +
            "- If backup already exists, both BACKUP and RESTORE buttons will be filled out.\n\n" +
            "- Only one backup is created per option and only the current files are backed up.\n\n" +
            "- Creating a backup again will overwrite any files in the current backup if it exists.\n\n" +
            "- Restoring files from the current backup will not remove any files from the backup.\n\n" +
            "- Using any available BACKUP / RESTORE / REMOVE options may require you to\n" +
            "  run Scanner111 in admin mode (and with a Mod Organizer 2 shortcut if using MO2).\n\n" +
            "If you have trouble running this program or wish to submit your crash log files\n" +
            "for help from our support team, join the Collective Modding Discord server.";

        public const string UpdatePopupText =
            "New version available! Press OK to open the GitHub Page.\n\n" +
            "Scanner111 GitHub : https://github.com/yourusername/Scanner111/releases/latest";

        public const string UpdateWarning =
            "❌ WARNING : YOUR SCANNER111 VERSION IS OUT OF DATE!\n" +
            "YOU CAN GET THE LATEST VERSION FROM HERE:\n" +
            "https://github.com/yourusername/Scanner111/releases/latest";

        public const string UpdateUnable =
            "❌ WARNING : SCANNER111 WAS UNABLE TO CHECK FOR UPDATES AT THIS TIME, TRY AGAIN LATER\n" +
            "CHECK FOR NEW VERSIONS HERE: https://github.com/yourusername/Scanner111/releases/latest";

        public const string AutoscanTextFallout4 =
            "FOR FULL LIST OF MODS THAT CAUSE PROBLEMS, THEIR ALTERNATIVES AND DETAILED SOLUTIONS\n" +
            "VISIT THE BUFFOUT 4 CRASH ARTICLE: https://www.nexusmods.com/fallout4/articles/3115\n" +
            "===============================================================================";
        #endregion
    }
}