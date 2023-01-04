using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRCAudioLink;
using VRC.SDK3.Components;
using System;
using UnityEditor;
using TMPro;
using static UnityEngine.Mathf;

public class ShareTrackSettings : UdonSharpBehaviour
{
    public AudioLink AL;
    public AudioLinkController ALcont;
    public PaletteSelectorGUI PaletteGUI;
    public VRCUrlInputField VRCinput;
    public InputField copyableField, testField;
    public HUDController HUD;
    public BoxCollider ALpanelCollider;
    //public TextMeshProUGUI testVar;

    VRCPlayerApi playerlocal;
    //VRCPlayerApi objectOwner;

// This denotes the 'version' of the OrbURL data structure
// if data structures are changed between versions we can detect an older version and fall back to a legacy mode
    const byte OrbURL_Version = 3;

// Precision of our representation of AudioLink slider values
    const byte AL_FullPrecision = 64;
    const byte AL_HalfPrecision = 32;
    const byte AL_QuaterPrecision = 16;

// Constant scaling factors for the AudioLink crossover values
// these are used to remap the values to the correct ranges
    const float x0_fact = 380.95237f;

    const float x1_min = 0.242f;
    const float x1_fact = 441.3793f;

    const float x2_min = 0.461f;
    const float x2_fact = 383.23352f;

    const float x3_min = 0.704f;
    const float x3_fact = 257.0281f;

// Constant values representing the defaults of some AL settings
    const float x0_def = 0.0f;
    const float x1_def = 0.25f;
    const float x2_def = 0.5f;
    const float x3_def = 0.75f;

    const float AL_Threshold_def = 0.45f;

    const float AL_Gain_def = 1f;
    const float AL_Trebble_def = 1f;
    const float AL_Bass_def = 1f;

    const float AL_HitFadeLength_def = 0.85f;
    const float AL_HitFadeExpFalloff_def = 0.15f;


// Keep legacy constants around for OrbURLS versions older than 3
    const byte LEGACY_AL_FullPrecision = 50;
    const byte LEGACY_AL_HalfPrecision = 25;

    const float LEGACY_x0_fact = 297.61905f;
    const float LEGACY_x1_fact = 344.82759f;
    const float LEGACY_x2_fact = 299.4012f;
    const float LEGACY_x3_fact = 200.80321f;

    const float LEGACY_AL_HitFadeLength_def = 0.9f;

// AudioLink settings
    byte AL_Threshold0 = 45;
    byte AL_Threshold1 = 45;
    byte AL_Threshold2 = 45;
    byte AL_Threshold3 = 45;

    byte AL_x0 = 0;
    byte AL_x1 = 25;
    byte AL_x2 = 50;
    byte AL_x3 = 75;

    byte AL_Gain = 50;
    byte AL_Trebble = 50;
    byte AL_Bass = 50;

    byte AL_HitFadeLength = 85;
    byte AL_HitFadeExpFalloff = 15;

// AudioLink proxy settings
// used for saving settings before deferring the application of said settings
    float[] AL_Proxy = new float[16];                                                                                                                                                             

// Main gui panel setting
    byte PGUI_Palette = 0;
    bool PGUI_PaletteEnabled = false;
    bool PGUI_Trails = true;
    bool PGUI_HistoryTrails = true;
    bool PGUI_HueShift = true;
    bool PGUI_PScroll = false;
    bool PGUI_PCycle = false;
    bool PGUI_VolRays = true;

// Previous settings, used to check if we actually need to update the copyable string or not
    bool valueChanged = true;
    string lastURL = "";

    byte ALp_Threshold0 = 0;
    byte ALp_Threshold1 = 0;
    byte ALp_Threshold2 = 0;
    byte ALp_Threshold3 = 0;

    byte ALp_x0 = 150;
    byte ALp_x1 = 150;
    byte ALp_x2 = 150;
    byte ALp_x3 = 150;

    byte ALp_Gain = 100;
    byte ALp_Trebble = 100;
    byte ALp_Bass = 100;

    byte ALp_HitFadeLength = 0;
    byte ALp_HitFadeExpFalloff = 0;

    byte PGUIp_Palette = 99;
    bool PGUIp_PaletteEnabled = false;
    bool PGUIp_Trails = true;
    bool PGUIp_HistoryTrails = true;
    bool PGUIp_HueShift = true;
    bool PGUIp_PScroll = false;
    bool PGUIp_PCycle = false;
    bool PGUIp_VolRays = true;

// URL field and custom settings strings
    string inField = "";
    string URL = "";
    string CustomSettings = "";

// Synced Udon variables
    [UdonSynced] public string NetworkSettings = "";
    [UdonSynced] public string NetworkURL = "";
    [UdonSynced] string OrbURL_Owner = "";
    [UdonSynced] bool isPlaylistOrbURL = false;
    [UdonSynced] bool isOrbURL = false;
// Keep a running count of how many syncs we've done
    [UdonSynced] public uint packetNum = 0;
    uint lastPacketNum = 0;

    bool FirstDataReceive = true;

// Update loop timer stuff
    const float copyFieldUpdateRate = 1.0f;
    float copyFieldUpdateTimer = 0.0f;

    const float panelDisableInterval = 5.0f;
    float panelDisableTimer = 5.0f;
    bool timerFinished = true;

    const float panelUpdateDelay = 1.0f;
    float panelUpdateTimer = 0f;
    bool doPanelUpdate = false;

// Lookup table for converting a number to a character for encoding to a copyable string
//      > Index range is 0-64, so we can fill exactly 6 bits of data
    char[] ConversionLUT = {
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',        
    'a', 'A', 'b', 'B', 'c', 'C', 'd', 'D', 'e', 'E',
    'f', 'F', 'g', 'G', 'h', 'H', 'i', 'I', 'j', 'J', 
    'k', 'K', 'l', 'L', 'm', 'M', 'n', 'N', 'o', 'O', 
    'p', 'P', 'q', 'Q', 'r', 'R', 's', 'S', 't', 'T', 
    'u', 'U', 'v', 'V', 'w', 'W', 'x', 'X', 'y', 'Y',
    'z', '#', '@', '%', '&', '!'
    };

    char[] LEGACY_ConversionLUT = {
    'a', 'A', 'b', 'B', 'c', 'C', 'd', 'D', 'e', 'E',
    'f', 'F', 'g', 'G', 'h', 'H', 'i', 'I', 'j', 'J', 
    'k', 'K', 'l', 'L', 'm', 'M', 'n', 'N', 'o', 'O', 
    'p', 'P', 'q', 'Q', 'r', 'R', 's', 'S', 't', 'T', 
    'u', 'U', 'v', 'V', 'w', 'W', 'x', 'X', 'y', 'Y',
    'z', '!', 
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'     
    };

    //char[] SevenBitLUT = new char[128];

    void Start()
    {
        GetCustomizationValues();
        playerlocal = Networking.LocalPlayer;
    }

// Packs 8 booleans (bits) into 1 byte
    byte ByteBitPacker(bool[] bits)
    {
        byte packedBits = 0;
        byte iter = (byte) Min(bits.Length, 8);

        for (int b=0; b<iter; b++) {
            if (bits[b]) packedBits |= (byte) (1 << b);
        }
        return packedBits;
    }

    ulong LongChunkPacker(byte[] inBytes)
    {
        ulong packedLong = 0;

        for (int b=0; b<inBytes.Length; b++) {
            ulong templong = inBytes[b];
            packedLong |= (templong << (b * 6));
        }

        return packedLong;
    }


///<summary>
/// Unpacks an arbitrary amount of bits (Booleans) stored in an int.
///</summary>
    bool[] BitUnpacker(int packedBits, int numBits)
    {
        bool[] bits = new bool[numBits];

        for (int b=0; b<numBits; b++) {
            bits[b] = (packedBits & (1 << b)) != 0;
        }
        //Array.Reverse(bits);
        return bits;
    }

// Fetch current customization values and remap them to the correct ranges before conversion
    public void GetCustomizationValues()
    {
    // The encoding system isn't precise enough to accurately represent some of the default values,
    // so in these cases we'll just make exceptions for them.
        byte overFlow = AL_FullPrecision + 1;

        if (AL.threshold0 == AL_Threshold_def) 
            AL_Threshold0 = overFlow;
        else 
            AL_Threshold0 = (byte)(AL.threshold0 * AL_FullPrecision);

        if (AL.threshold1 == AL_Threshold_def) 
            AL_Threshold1 = overFlow;
        else 
            AL_Threshold1 = (byte)(AL.threshold1 * AL_FullPrecision);

        if (AL.threshold2 == AL_Threshold_def) 
            AL_Threshold2 = overFlow;
        else 
            AL_Threshold2 = (byte)(AL.threshold2 * AL_FullPrecision);

        if (AL.threshold3 == AL_Threshold_def) 
            AL_Threshold3 = overFlow;
        else 
            AL_Threshold3 = (byte)(AL.threshold3 * AL_FullPrecision);                                


    // AudioLink crossover values
        if (AL.x0 == x0_def) 
            AL_x0 = overFlow;
        else 
            AL_x0 = (byte)(AL.x0 * x0_fact);

        if (AL.x1 == x1_def) 
            AL_x1 = overFlow;
        else 
            AL_x1 = (byte)((AL.x1 - x1_min) * x1_fact);       

        if (AL.x2 == x2_def) 
            AL_x2 = overFlow;
        else 
            AL_x2 = (byte)((AL.x2 - x2_min) * x2_fact);

        if (AL.x3 == x3_def) 
            AL_x3 = overFlow;
        else 
            AL_x3 = (byte)((AL.x3 - x3_min) * x3_fact);


    // AudioLink level values
        AL_Gain = (byte)(AL.gain * AL_HalfPrecision);
        AL_Bass = (byte)(AL.bass * AL_HalfPrecision);
        AL_Trebble = (byte)(AL.treble * AL_HalfPrecision);
        
    // AudioLink fade values
        AL_HitFadeLength = (byte)(AL.fadeLength * AL_FullPrecision);

        if (AL.fadeExpFalloff == AL_HitFadeExpFalloff_def)
            AL_HitFadeExpFalloff = overFlow;
        else
            AL_HitFadeExpFalloff = (byte)(AL.fadeExpFalloff * AL_FullPrecision);

    // AudioOrbs customization values
        PGUI_Palette = PaletteGUI.PALETTE;
        PGUI_PaletteEnabled = PaletteGUI.CUSTOMPALETTE;
        PGUI_Trails = PaletteGUI.TRAILS;
        PGUI_HistoryTrails = PaletteGUI.HISTORYTRAILS;
        PGUI_HueShift = PaletteGUI.HUESHIFT;
        PGUI_PCycle = PaletteGUI.PCYCLE;
        PGUI_PScroll = PaletteGUI.PSCROLL;
        PGUI_VolRays = PaletteGUI.VRAYS;
    }

// Sends data over the network
    void SendData(bool orbURL, bool playlistMode, string URL)
    {
        isOrbURL = orbURL;
        isPlaylistOrbURL = playlistMode;
        if (!isOrbURL) 
        { 
            ConvertCustomizationValues(true);
            NetworkURL = URL;
            RequestSerialization(); 
        }
        else
        {
            NetworkSettings = CustomSettings;
            OrbURL_Owner = playerlocal.displayName;
            NetworkURL = URL;
            RequestSerialization(); 
        }
    }

// Encode a setting value to a character
    char Encode(byte idx)
    {
        return ConversionLUT[idx];
    }

// Decode a character to a usable float value
    float DecodeFloat(char c, byte range, float defVal, bool isLegacy = false)
    {
        if (c == '!') return defVal;
        
        float convertedVal = (float)System.Array.IndexOf(isLegacy ? LEGACY_ConversionLUT : ConversionLUT, c) / (float)range;
        return convertedVal;
    }

// Decode a character to a usable float value, and remap it to the correct range
    float DecodeRemapFloat(char c, float scale, float offset, float defVal, bool isLegacy = false)
    {
        if (c == '!') return defVal;

        float convertedVal = ( (float)System.Array.IndexOf(isLegacy ? LEGACY_ConversionLUT : ConversionLUT, c) / scale ) + offset;
        return convertedVal;
    }

// Decode a character to a usable integer byte value
    byte DecodeByte(char c, bool isLegacy = false)
    {
        return (byte)System.Array.IndexOf(isLegacy ? LEGACY_ConversionLUT : ConversionLUT, c);
    }

// Decode a character to a usable boolean value
    bool DecodeBool(char c)
    {
        return (c == '1');
    }

// Encode all setting values and lump them into a string
    public void ConvertCustomizationValues(bool retrieve)
    {
        if (retrieve) GetCustomizationValues();

        bool[] bitPack6_01 = {
            PGUI_Trails, PGUI_HueShift, PGUI_PScroll, PGUI_PCycle, PGUI_HistoryTrails, PGUI_VolRays
        };

        CustomSettings = String.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}{11}{12}{13}{14}{15}", 
            OrbURL_Version,                                                                                         // Data structure ver
            Encode(AL_Threshold0), Encode(AL_Threshold1), Encode(AL_Threshold2), Encode(AL_Threshold3),             // AL thresholds
            Encode(AL_x0), Encode(AL_x1), Encode(AL_x2), Encode(AL_x3),                                             // AL crossover values
            Encode(AL_Gain), Encode(AL_Bass), Encode(AL_Trebble),                                                   // AL balancing sliders
            Encode(AL_HitFadeLength), Encode(AL_HitFadeExpFalloff),                                                 // AL hit sliders
            PGUI_PaletteEnabled ? Encode(PGUI_Palette) : 'Y',                                                       // AO palette
            Encode( ByteBitPacker(bitPack6_01) ) );                                                                 // AO settings

    }

// Callable wrapper for LoadCustomization values
// this is needed because we can't use arguments from UI event triggers
    public void UserInputURL()
    {
        LoadCustomizationValues(false);
        packetNum++;
    }

// Load values from the synced string
    public void LoadCustomizationValues(bool isPlaylist)
    {
        string tempString;
        string[] splitStr;
        char[] splitChar = {'#'};

        if (!isPlaylist)
        {
            Networking.SetOwner(playerlocal, gameObject);

            VRCUrl tempURL = VRCinput.GetUrl();
            tempString = tempURL.ToString();        
            splitStr = tempString.Split(splitChar, 2);
        }
        else
        {
            tempString = NetworkURL;
            splitStr = tempString.Split(splitChar, 2);
        }

        if (splitStr[0].Length > 0)
            URL = splitStr[0];
        
    // Exit if the user did not input a valid URL
        int urlTest = URL.IndexOf("://", System.StringComparison.Ordinal);
        if (urlTest < 1 || urlTest > 8) return;

        if (splitStr.Length == 1) {
            SendData(false, isPlaylist, URL);
            //HUD.NOTIF_OrbURLFail();      
            return; 
        }

        CustomSettings = splitStr[1];

    // The first char in the settings string denotes which data structure version the OrbURL was created with
        byte loaded_OrbURL_ver = (byte)Char.GetNumericValue(CustomSettings[0]);
        bool isLegacy = (loaded_OrbURL_ver < OrbURL_Version);

    // Version-specific error checking
        if (loaded_OrbURL_ver < OrbURL_Version) {       // If a lower version has the wrong string length, throw error
            if (CustomSettings.Length < 19) {
                SendData(false, isPlaylist, URL);
                HUD.NOTIF_OrbURLFail();
                return;
            }
        }
        else if (loaded_OrbURL_ver > OrbURL_Version) {  // If the OrbURL version is somehow greater than the world's, throw error
            SendData(false, isPlaylist, URL);
            HUD.NOTIF_OrbURLFail();
            return; 
        }
        else {                                          // If a correct version has the wrong string length, throw error
            if (CustomSettings.Length < 15) {
                SendData(false, isPlaylist, URL);
                HUD.NOTIF_OrbURLFail();
                return;
            }
        }

    // Check for invalid characters in the settings string
    // if any invalid characters are found, exit and push an error
        for (int c=0; c < CustomSettings.Length; c++) {
            int validTest = System.Array.IndexOf(ConversionLUT, CustomSettings[c]);
            if ( validTest == -1 || validTest == Int32.MaxValue) {
                SendData(false, isPlaylist, URL);
                HUD.NOTIF_OrbURLFail();
                return;
            }
        }     

        if (isPlaylist) HUD.NOTIF_OrbURLPlaylist();
        else            HUD.NOTIF_OrbURLSuccess("N/A", true);

        DisableAudioLinkPanel();

    // Save AudioLink settings so we can properly defer applying them
        byte FullPrecision = isLegacy ? LEGACY_AL_FullPrecision : AL_FullPrecision;
        byte HalfPrecision = isLegacy ? LEGACY_AL_HalfPrecision : AL_HalfPrecision;
        float x0scale = isLegacy ? LEGACY_x0_fact : x0_fact;
        float x1scale = isLegacy ? LEGACY_x1_fact : x1_fact;
        float x2scale = isLegacy ? LEGACY_x2_fact : x2_fact;
        float x3scale = isLegacy ? LEGACY_x3_fact : x3_fact;

        AL_Proxy[0] = DecodeFloat(CustomSettings[1], FullPrecision, AL_Threshold_def, isLegacy);
        AL_Proxy[1] = DecodeFloat(CustomSettings[2], FullPrecision, AL_Threshold_def, isLegacy);
        AL_Proxy[2] = DecodeFloat(CustomSettings[3], FullPrecision, AL_Threshold_def, isLegacy);
        AL_Proxy[3] = DecodeFloat(CustomSettings[4], FullPrecision, AL_Threshold_def, isLegacy);

        AL_Proxy[4] = DecodeRemapFloat(CustomSettings[5], x0scale, 0f, x0_def, isLegacy);
        AL_Proxy[5] = DecodeRemapFloat(CustomSettings[6], x1scale, x1_min, x1_def, isLegacy);
        AL_Proxy[6] = DecodeRemapFloat(CustomSettings[7], x2scale, x2_min, x2_def, isLegacy);
        AL_Proxy[7] = DecodeRemapFloat(CustomSettings[8], x3scale, x3_min, x3_def, isLegacy);

        AL_Proxy[8] = DecodeFloat(CustomSettings[9], HalfPrecision, AL_Gain_def, isLegacy);
        AL_Proxy[9] = DecodeFloat(CustomSettings[10], HalfPrecision, AL_Bass_def, isLegacy);
        AL_Proxy[10] = DecodeFloat(CustomSettings[11], HalfPrecision, AL_Trebble_def, isLegacy);

        AL_Proxy[11] = DecodeFloat(CustomSettings[12], FullPrecision, AL_HitFadeLength_def, isLegacy);
        AL_Proxy[12] = DecodeFloat(CustomSettings[13], FullPrecision, AL_HitFadeExpFalloff_def, isLegacy);
    


        DeferUpdateAudioLinkPanelValues(); // Apply loaded AudioLink panel settings

        //Debug.Log("Step 2");
        LoadAudioOrbValues(true); // Apply loaded AudioOrbs settings for local player
    
    // Send out data to remote players
        SendData(true, isPlaylist, URL);
    }

// Apply loaded AudioOrbs settings for remote players
    public override void OnDeserialization()
    {
        Debug.Log($"local packet:   {lastPacketNum}");
        Debug.Log($"network packet: {packetNum}");
        if (lastPacketNum == packetNum) return;

        lastPacketNum = packetNum;

        UpdateCopyableField(true);
        copyFieldUpdateTimer = 0.0f;

        if (!isOrbURL) return;

        LoadAudioOrbValues(false);

        if (isPlaylistOrbURL) 
            HUD.NOTIF_OrbURLPlaylist();
        else 
            HUD.NOTIF_OrbURLSuccess(OrbURL_Owner, false);

        DisableAudioLinkPanel();
    }

// Updates audio link panel with a delay, so values aren't accidentally changed immediately
    void DeferUpdateAudioLinkPanelValues()
    {
        panelUpdateTimer = 0f;
        doPanelUpdate = true;
    }

// Apply loaded AudioLink settings to slider values and tell AudioLink to update
// these will automatically sync over the network because AudioLink settings use continuous syncing
    void LoadAudioLinkValues()
    {
        ALcont.threshold0Slider.value = AL_Proxy[0];
        ALcont.threshold1Slider.value = AL_Proxy[1];
        ALcont.threshold2Slider.value = AL_Proxy[2];
        ALcont.threshold3Slider.value = AL_Proxy[3];

        ALcont.x0Slider.value = AL_Proxy[4];
        ALcont.x1Slider.value = AL_Proxy[5];
        ALcont.x2Slider.value = AL_Proxy[6];
        ALcont.x3Slider.value = AL_Proxy[7];

        ALcont.gainSlider.value = AL_Proxy[8];
        ALcont.bassSlider.value = AL_Proxy[9];
        ALcont.trebleSlider.value = AL_Proxy[10];

        ALcont.fadeLengthSlider.value = AL_Proxy[11];
        ALcont.fadeExpFalloffSlider.value = AL_Proxy[12];

        ALcont.UpdateSettings();
        AL.UpdateSettings();
    }

// Apply loaded AudioOrbs settings and tell AudioOrbs UI to update
//      > called on remote players, too, since unlike the AL panel it is not normally synced
//      > detects and handles legacy versions, so old OrbURLs don't fail to load
    void LoadAudioOrbValues(bool local)
    {
    // Prevent script crashing on first load
        if (!local) {
            if (NetworkSettings.Length < 5) return;

            byte loaded_OrbURL_ver = (byte)Char.GetNumericValue(NetworkSettings[0]);

            if ( (loaded_OrbURL_ver < OrbURL_Version) && NetworkSettings.Length < 20) return;
            if ( (loaded_OrbURL_ver == OrbURL_Version) && NetworkSettings.Length < 16) return;
        }

        string tempSettings;

        if (local) tempSettings = CustomSettings;
        else tempSettings = NetworkSettings;

    // Parse OrbURL version so we can handle legacy versions correctly
        byte loadedVer = (byte)Char.GetNumericValue(tempSettings[0]);        

        byte paletteCheck = DecodeByte(tempSettings[14], (byte)Char.GetNumericValue(tempSettings[0]) < OrbURL_Version);
        if (paletteCheck < 48)
        { 
            PaletteGUI.CUSTOMPALETTE = true; 
            PaletteGUI.PALETTE = paletteCheck;
        }

        if (loadedVer < OrbURL_Version) {
            PaletteGUI.TRAILS = DecodeBool(tempSettings[15]);
            PaletteGUI.HUESHIFT = DecodeBool(tempSettings[16]);
            PaletteGUI.PSCROLL = DecodeBool(tempSettings[17]);
            PaletteGUI.PCYCLE = DecodeBool(tempSettings[18]);

            if (loadedVer == 2) {
                PaletteGUI.HISTORYTRAILS = DecodeBool(tempSettings[19]);
            }
        }
        else {
            bool[] loadedSettings = BitUnpacker( DecodeByte(tempSettings[15]), 6 );

            PaletteGUI.TRAILS = loadedSettings[0];
            PaletteGUI.HUESHIFT = loadedSettings[1];
            PaletteGUI.PSCROLL = loadedSettings[2];
            PaletteGUI.PCYCLE = loadedSettings[3];
            PaletteGUI.HISTORYTRAILS = loadedSettings[4];
            PaletteGUI.VRAYS = loadedSettings[5];
        }

        PaletteGUI.UpdateSettings();
    }

    public void DebugShow()
    {
        VRCUrl test = VRCinput.GetUrl();
        string tempString = test.ToString();
        Debug.Log(tempString.Split('#')[1]);
    }

// Updates the copyfield with the current settings
    public void UpdateCopyableField(bool init)
    {
        GetCustomizationValues();

    // Check if values have actually changed
        valueChanged = (
            AL_Threshold0 != ALp_Threshold0 ||
            AL_Threshold1 != ALp_Threshold1 ||
            AL_Threshold2 != ALp_Threshold2 ||
            AL_Threshold3 != ALp_Threshold3 ||
            AL_x0 != ALp_x0 || AL_x1 != ALp_x1 || AL_x2 != ALp_x2 || AL_x3 != ALp_x3 ||
            AL_Gain != ALp_Gain || AL_Trebble != ALp_Trebble || AL_Bass != ALp_Bass ||
            AL_HitFadeLength != ALp_HitFadeLength || AL_HitFadeExpFalloff != ALp_HitFadeExpFalloff ||
            PGUI_Palette != PGUIp_Palette || PGUI_PaletteEnabled != PGUIp_PaletteEnabled || PGUI_Trails != PGUIp_Trails || 
            PGUI_HueShift != PGUIp_HueShift || PGUI_PScroll != PGUIp_PScroll || PGUI_PCycle != PGUIp_PCycle || 
            PGUI_HistoryTrails != PGUIp_HistoryTrails || PGUI_VolRays != PGUIp_VolRays ||
            NetworkURL != lastURL
            );

        if (!valueChanged) return; // If values have not changed since last time we can simply ignore them and exit

        Debug.Log("Values changed! Updating field...");

    // Assign previous value variables
        ALp_Threshold0 = AL_Threshold0;
        ALp_Threshold1 = AL_Threshold1;
        ALp_Threshold2 = AL_Threshold2;
        ALp_Threshold3 = AL_Threshold3;

        ALp_x0 = AL_x0;
        ALp_x1 = AL_x1;
        ALp_x2 = AL_x2;
        ALp_x3 = AL_x3;

        ALp_Gain = AL_Gain;
        ALp_Trebble = AL_Trebble;
        ALp_Bass = AL_Bass;

        ALp_HitFadeLength = AL_HitFadeLength;
        ALp_HitFadeExpFalloff = AL_HitFadeExpFalloff;

        PGUIp_Palette = PGUI_Palette;
        PGUIp_PaletteEnabled = PGUI_PaletteEnabled;
        PGUIp_Trails = PGUI_Trails;
        PGUIp_HistoryTrails = PGUI_HistoryTrails;
        PGUIp_HueShift = PGUI_HueShift;
        PGUIp_PScroll = PGUI_PScroll;
        PGUIp_PCycle = PGUI_PCycle;
        PGUIp_VolRays = PGUI_VolRays;

        lastURL = NetworkURL;

        if (init) {
            copyableField.text = $"{NetworkURL}#{NetworkSettings}";
            return;
        }

        ConvertCustomizationValues(false);

        copyableField.text = $"{NetworkURL}#{CustomSettings}";
    }

// Temporarily disables input on the AudioLink panel to avoid loaded settings being accidentally overwritten
    void DisableAudioLinkPanel()
    {
        ALpanelCollider.enabled = false;
        timerFinished = false;
        panelDisableTimer = 0.0f;
    }

    void Update()
    {
    // Copyfield update timer
        if (copyFieldUpdateTimer < copyFieldUpdateRate) { 
            copyFieldUpdateTimer += Time.deltaTime; 
        }
        else {  
            UpdateCopyableField(false);
            copyFieldUpdateTimer = 0.0f;
        }

    // AudioLink panel disable timer
        if (!timerFinished) {
            if (panelDisableTimer < panelDisableInterval)
                panelDisableTimer += Time.deltaTime;
            else {
                ALpanelCollider.enabled = true;
                timerFinished = true;
            }
        }

    // Deferred AudioLink panel update
        if (doPanelUpdate) {
            if (panelUpdateTimer < panelUpdateDelay)
                panelUpdateTimer += Time.deltaTime;
            else {
                LoadAudioLinkValues();
                doPanelUpdate = false;
            }
        }
    }
}
