using FsCheck.Xunit;

namespace Tests.Unit;

public class FsCheckSmokeTests
{
    [Property]
    public bool IntegerAddition_IsCommutative(int a, int b) => a + b == b + a;
}
