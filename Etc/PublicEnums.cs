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
    NOTE_ON, NOTE_OFF, NOTE_OFF_ENV, ENV_RELEASE, INSTRUMENT, 
    PANNING, PRE_PORTA, VIBRATO, VIB_RANGE, VIB_SHAPE, PITCH, 
    HINT_ARPEGGIO, HINT_VOLUME, VOL_SLIDE, HINT_PORTA, HINT_LEGATO,
    HINT_ARP_TIME,
    
    NO_NOTE_ON, SEARCH_STOPPED,
    INVALID,
}

public enum CmdStructField
{
    TICK, CHANNEL, CMD_TYPE, VALUE1, VALUE2
}