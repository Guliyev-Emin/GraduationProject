using System.Collections.Generic;

namespace GraduationProject.Controllers.IModels;

public interface IUserPoint:IPoint
{
    public bool PointStatus { get; set; }
    public int PointCount { get; set; }
    public List<string> PointCoordinates { get; set; }
}