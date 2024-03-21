namespace Furnace2MML.Etc;

public enum InstOperator
{
    EN, AM, AR, DR, MULT, RR, SL, TL, DT2, RS, DT, D2R, SSG_EG, DAM, DVB, EGT, KSL, SUS, VIB, WS, KSR, TL_VC
}

public enum OutputFileType
{
    CMD_STREAM, TXT_OUTPUT
}

public enum EffectValueType
{
    UNUSED, XX, XY 
}

public enum CmdType
{
    NOTE_ON, NOTE_OFF, HINT_LEGATO, HINT_PORTA, PRE_PORTA,
    HINT_VOLUME, HINT_ARPEGGIO, INSTRUMENT, PANNING,
    HINT_ARP_TIME,
    
    NO_NOTE_ON, SEARCH_STOPPED
}