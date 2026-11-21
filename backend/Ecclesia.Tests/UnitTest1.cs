using Ecclesia.Api;
namespace Ecclesia.Tests;
public class GeoTests {
 [Fact] public void SamePointHasZeroDistance() => Assert.Equal(0, Geo.Distance(50.6326, 5.5797, 50.6326, 5.5797), 8);
 [Fact] public void DistanceIsSymmetric() => Assert.Equal(Geo.Distance(50.63, 5.58, 50.85, 4.35), Geo.Distance(50.85, 4.35, 50.63, 5.58), 8);
 [Fact] public void LiegeToBrusselsIsAboutNinetyKilometres() => Assert.InRange(Geo.Distance(50.6326, 5.5797, 50.8503, 4.3517), 85, 95);
}