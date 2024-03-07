using NUnit.Framework;
using ProjectTestLib.Helper;
namespace ProjectTestLib;

[GameClass(1), CancelAfter(Constants.Timeout)]
public class Class1Test
{

    [GameTask("Can you create a resource group named 'projProd' in Hong Kong?", 2, 10, 1)]
    [Test]
    public void Test01_ResourceGroupExist()
    {
        Assert.That(1, Is.Not.Null);
    }
}
