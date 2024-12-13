using System;
using System.Runtime.CompilerServices;

public class RollingCodeGenerator
{
    private byte RollingIndex;

    public RollingCodeGenerator()
    {
        RollingIndex = 0;
    }

    public byte GetIndex()
    {
        //rolling code del packid
        if (RollingIndex < 7) RollingIndex++;
        else RollingIndex = 0;
        return RollingIndex;
    }
}
