using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GraduationProject.Controllers;
using GraduationProject.Controllers.IModels;
using GraduationProject.Controllers.Model;
using JetBrains.Annotations;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace GraduationProject.Construction
{
    [UsedImplicitly]
    public class Reader : Connection
    {
        public static List<SketchInfo> SketchInfos;
        public static TreeNode TreeNode;
        public static string ModelProperties;
        private static bool _check;
        private static double _deepth;
        private static Feature _featureNode;
        private static List<string> _pointCoordinates;
        private static List<string> _lineCoordinates;
        private static List<short> _lineTypes;
        private static List<double> _lineLengths;
        private static List<string> _arcCoordinates;
        private static List<double> _arcRadius;
        private static List<string> _ellipseCoordinates;
        private static List<string> _parabolaCoordinates;

        /// <summary>
        ///     Функция по нахождению узлов в проекте SolidWorks.
        /// </summary>
        /// <param name="rootNode">Узел</param>
        /// <returns>Возвращает узлы дерева проекта SolidWorks и свойства узлов.</returns>
        public static TreeNode ProjectReading(TreeControlItem rootNode)
        {
            var nodeObjectType = rootNode.ObjectType;
            var nodeObject = rootNode.Object;
            var nodeType = "";
            var nodeName = "";

            if (nodeObject != null)
            {
                switch (nodeObjectType)
                {
                    case (int) swTreeControlItemType_e.swFeatureManagerItem_Feature:
                        _featureNode = (Feature) nodeObject;
                        nodeType = _featureNode.GetTypeName();
                        nodeName = _featureNode.Name;
                        break;
                }

                if (_check & !nodeType.Equals("DetailCabinet") & !nodeType.Equals("MaterialFolder") &
                    !nodeType.Equals("HistoryFolder") & !nodeType.Equals("SensorFolder"))
                    TreeNode.LastNode.Nodes.Add(nodeName);
                else
                    TreeNode.Nodes.Add(nodeName);

                if (nodeType.Equals("ProfileFeature"))
                    SketchListener(nodeName);
                rootNode.Expanded = false;
            }

            var childNode = rootNode.GetFirstChild();
            _check = childNode != null;
            while (childNode != null && !nodeType.Equals("HistoryFolder") && !nodeType.Equals("DetailCabinet"))
            {
                ProjectReading(childNode);
                childNode = childNode.GetNext();
            }

            return TreeNode;
        }

        /// <summary>
        ///     Процедура позволяющая извлекать значения координат двухмерных объектов в эскизе.
        /// </summary>
        /// <param name="sketch">Название эскиза.</param>
        private static void SketchListener(string sketch)
        {
            var selectedSketch = (Sketch) _featureNode.GetSpecificFeature2();
            var feature = _featureNode.GetOwnerFeature();
            var lineCount = selectedSketch.GetLineCount();
            var arcCount = selectedSketch.GetArcCount();
            var ellipseCount = selectedSketch.GetEllipseCount();
            var parabolaCount = selectedSketch.GetParabolaCount();
            var pointCount = selectedSketch.GetUserPointsCount();
            _deepth = new double();
            _pointCoordinates = new List<string>();
            _lineCoordinates = new List<string>();
            _lineTypes = new List<short>();
            _lineLengths = new List<double>();
            _arcCoordinates = new List<string>();
            _arcRadius = new List<double>();
            _ellipseCoordinates = new List<string>();
            _parabolaCoordinates = new List<string>();

            DeepthListener(feature);
            GetPlanesAndFaces(selectedSketch);

            if (lineCount != 0)
                LineListener(selectedSketch, lineCount);

            if (ellipseCount != 0)
                EllipseListener(selectedSketch, ellipseCount);

            if (arcCount != 0)
                ArcListener(selectedSketch, arcCount);

            if (parabolaCount != 0)
                ParabolaListener(selectedSketch, parabolaCount);

            if (pointCount != 0)
                PointListener(selectedSketch, pointCount);

            SketchInfos.Add(new SketchInfo
            {
                SketchName = sketch, Deepth = _deepth,
                PointStatus = pointCount != 0, PointCount = pointCount, PointCoordinates = _pointCoordinates,
                LineStatus = lineCount != 0, LineCount = lineCount, LineCoordinates = _lineCoordinates,
                LineTypes = _lineTypes, LineLengths = _lineLengths,
                ArcStatus = arcCount != 0, ArcCount = arcCount, ArcCoordinates = _arcCoordinates,
                ArcRadius = _arcRadius,
                EllipseStatus = ellipseCount != 0, EllipseCount = ellipseCount,
                EllipseCoordinates = _ellipseCoordinates,
                ParabolaStatus = parabolaCount != 0, ParabolaCount = parabolaCount,
                ParabolaCoordinates = _parabolaCoordinates
            });
        }

        /// <summary>
        /// Процедура позволяющая извлекать значения выдавливания эскиза. 
        /// </summary>
        /// <param name="feature">Объект типа IFeature</param>
        private static void DeepthListener(IFeature feature)
        {
            var featureName = feature.Name;
            var deDimension = (Dimension) ModelDoc2.Parameter("D1@" + featureName);
            if (deDimension is null) return;
            var deepth =
                (double[]) deDimension.GetSystemValue3((int) swInConfigurationOpts_e.swAllConfiguration,
                    featureName);
            _deepth = deepth[0] * 1000;
            TreeNode.LastNode.Nodes.Insert(0, @"Выдавливание: " + _deepth + @" мм");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sketch"></param>
        private static void GetPlanesAndFaces(ISketch sketch)
        {
            var transformationMatrix = sketch.ModelToSketchTransform;
            var transformationMatrixData = (double[]) transformationMatrix.ArrayData;
            var top = new double[] { 1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0, 1, 0, 0, 0 };
            //if (transformationMatrixData.SequenceEqual(top))
                //MessageBox.Show("Сверху");
            var s = transformationMatrixData.Aggregate("", (current, data) => current + "|" + data);
            //MessageBox.Show(s);
        }

        /// <summary>
        /// Процедура позволяющая извлекать значения координат точек. 
        /// </summary>
        /// <param name="sketch">Объект типа Sketch.</param>
        /// <param name="pointCount">Количество точек в эскизе</param>
        private static void PointListener(ISketch sketch, int pointCount)
        {
            var getPointProperties = sketch.GetUserPoints2();
            if (getPointProperties is not IEnumerable pointEnumerable) return;
            var point = pointEnumerable.Cast<double>().ToArray();
            for (var index = 0; index < pointCount; index++)
            {
                TreeNode.LastNode.LastNode.Nodes.Add("Точка");
                if (index == pointCount) continue;
                var pointCoordinate = "Координаты: x = " + point[8 * index + 5] * 1000 + ", y = " +
                                      point[8 * index + 6] * 1000 + ", z = " + point[8 * index + 7] * 1000 + ";";
                _pointCoordinates.Add(pointCoordinate);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(pointCoordinate);
            }
        }

        /// <summary>
        ///     Процедура позволяющая извлекать значения координат начальных, конечных, фокусных и вершин точек параболы.
        /// </summary>
        /// <param name="sketch">Объект типа Sketch.</param>
        /// <param name="parabolaCount">Количество параболов в эскизе.</param>
        private static void ParabolaListener(ISketch sketch, int parabolaCount)
        {
            var getParabolaProperties = sketch.GetParabolas2();
            if (getParabolaProperties is not IEnumerable parabolaEnumerable) return;
            var parabola = parabolaEnumerable.Cast<double>().ToArray();
            for (var i = 0; i < parabolaCount; i++)
            {
                TreeNode.LastNode.LastNode.Nodes.Add("Парабола");
                if (i == parabolaCount) continue;
                var start = "Начало: x = " + parabola[18 * i + 6] * 1000 + ", y = " + parabola[18 * i + 7] * 1000 +
                            ", z = " + parabola[18 * i + 8] * 1000 + ";";
                var end = "Конец: x = " + parabola[18 * i + 9] * 1000 + ", y = " + parabola[18 * i + 10] * 1000 +
                          ", z = " +
                          parabola[18 * i + 11] * 1000 + ";";
                var focusPoint = "Фокусная точка: x = " + parabola[18 * i + 12] * 1000 + ", y = " +
                                 parabola[18 * i + 13] * 1000 +
                                 ", z = " + parabola[18 * i + 14] * 1000 + ";";
                var apexPoint = "Точка вершины: x = " + parabola[18 * i + 15] * 1000 + ", y = " +
                                parabola[18 * i + 16] * 1000 +
                                ", z = " + parabola[18 * i + 17] * 1000 + ";";
                _parabolaCoordinates.Add(start + "\n" + end + "\n" + focusPoint + "\n" + apexPoint);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(start);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(end);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(focusPoint);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(apexPoint);
            }
        }

        /// <summary>
        ///     Процедура позволяющая извлекать значения координат начальных, конечных и центральных точек дуг.
        /// </summary>
        /// <param name="sketch">Объект типа Sketch.</param>
        /// <param name="arcCount">Количество дуг в эскизе.</param>
        private static void ArcListener(ISketch sketch, int arcCount)
        {
            var getArcsProperties = sketch.GetArcs2();
            if (getArcsProperties is not IEnumerable arcsEnumerable) return;
            var arcs = arcsEnumerable.Cast<double>().ToArray();

            var vSketchSeg = sketch.GetSketchSegments();
            var sketchSegEnum = (IEnumerable) vSketchSeg;
            var sketchSegments = sketchSegEnum.Cast<SketchSegment>().ToArray();
            for (var i = 0; i < arcCount; i++)
            {
                TreeNode.LastNode.LastNode.Nodes.Add("Дуга");
                var j = i;
                if (i == arcCount) continue;
                var start = "Начало: x = " + arcs[16 * i + 6] * 1000 + ", y = " + arcs[16 * i + 7] * 1000 +
                            ", z = " + arcs[16 * i + 8] * 1000 + ";";
                var end = "Конец: x = " + arcs[16 * i + 9] * 1000 + ", y = " + arcs[16 * i + 10] * 1000 + ", z = " +
                          arcs[16 * i + 11] * 1000 + ";";
                var center = "Центр: x = " + arcs[16 * i + 12] * 1000 + ", y = " + arcs[16 * i + 13] * 1000 +
                             ", z = " + arcs[16 * i + 14] * 1000 + ";";

                var sketchSegment = sketchSegments[j];

                while (sketchSegment.GetType() != (int) swSketchSegments_e.swSketchARC)
                {
                    j++;
                    sketchSegment = sketchSegments[j];
                }

                // ReSharper disable once SuspiciousTypeConversion.Global
                var arcSketch = (SketchArc) sketchSegment;
                var radius = arcSketch.GetRadius() * 1000;

                _arcRadius.Add(radius);
                _arcCoordinates.Add(start + "\n" + end + "\n" + center);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(center);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(start);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(end);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add("Радиус: " + radius + "мм");
            }
        }

        /// <summary>
        ///     Процедура позволяющая извлекать значения координат начальной и конечной точек отрезка.
        /// </summary>
        /// <param name="sketch">Объект типа Sketch.</param>
        /// <param name="lineCount">Количество линий в эскизе.</param>
        private static void LineListener(ISketch sketch, int lineCount)
        {
            ILine lines = new Line();

            var getLinesProperties = sketch.GetLines2(4);
            if (getLinesProperties is not IEnumerable lineEnumerable) return;
            var line = lineEnumerable.Cast<double>().ToArray();

            var vSketchSeg = sketch.GetSketchSegments();
            var sketchSegEnum = (IEnumerable) vSketchSeg;
            var sketchSegments = sketchSegEnum.Cast<SketchSegment>().ToArray();

            for (var i = 0; i < lineCount; i++)
            {
                TreeNode.LastNode.LastNode.Nodes.Add("Отрезок");
                var j = i;
                if (i == lineCount) continue;
                var lineStyle = (short) line[12 * i + 2];
                var start = "Начало: x = " + line[12 * i + 6] * 1000 + ", y = " + line[12 * i + 7] * 1000 +
                            ", z = " + line[12 * i + 8] * 1000 + ";";
                var end = "Конец: x = " + line[12 * i + 9] * 1000 + ", y = " + line[12 * i + 10] * 1000 + ", z = " +
                          line[12 * i + 11] * 1000 + ";";
                var sketchSegment = sketchSegments[j];
                while (sketchSegment.GetType() != (int) swSketchSegments_e.swSketchLINE)
                {
                    j++;
                    sketchSegment = sketchSegments[j];
                }

                var lineLength = sketchSegment.GetLength() * 1000.0;
                _lineTypes.Add(lineStyle);
                _lineCoordinates.Add(start + "\n" + end);
                _lineLengths.Add(lineLength);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add("Длина: " + lineLength + " мм");
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(start);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(end);
            }
        }

        /// <summary>
        ///     Процедура позволяющая извлекать значения координат начальной, конечной, центральной,
        ///     на большой оси X и на малой оси X точек эллипса.
        /// </summary>
        /// <param name="sketch">Объект типа Sketch.</param>
        /// <param name="ellipseCount">Количество эллипсов в эскизе.</param>
        private static void EllipseListener(ISketch sketch, int ellipseCount)
        {
            var getEllipseProperties = sketch.GetEllipses3();
            if (getEllipseProperties is not IEnumerable ellipseEnumerable) return;
            var ellipse = ellipseEnumerable.Cast<double>().ToArray();
            for (var i = 0; i < ellipseCount; i++)
            {
                TreeNode.LastNode.LastNode.Nodes.Add("Эллипс");
                if (i == ellipseCount) continue;
                var start = "Начало: x = " + ellipse[16 * i + 6] * 1000 + ", y = " + ellipse[16 * i + 7] * 1000 +
                            ", z = " + ellipse[16 * i + 8] * 1000 + ";";
                var end = "Конец: x = " + ellipse[16 * i + 9] * 1000 + ", y = " + ellipse[16 * i + 10] * 1000 +
                          ", z = " +
                          ellipse[16 * i + 11] * 1000 + ";";
                var center = "Центр: x = " + ellipse[16 * i + 12] * 1000 + ", y = " + ellipse[16 * i + 13] * 1000 +
                             ", z = " + ellipse[16 * i + 14] * 1000 + ";";
                var majorPoint = "Точка на большой оси: x = " + ellipse[16 * i + 15] * 1000 + ", y = " +
                                 ellipse[16 * i + 16] * 1000 + ", z = " +
                                 ellipse[16 * i + 17] * 1000 + ";";
                var minorPoint = "Точка на малой оси: x = " + ellipse[16 * i + 18] * 1000 + ", y = " +
                                 ellipse[16 * i + 19] * 1000 + ", z = " +
                                 ellipse[16 * i + 20] * 1000 + ";";
                _ellipseCoordinates.Add(start + "\n" + end + "\n" + center + "\n" + majorPoint + "\n" + minorPoint);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(center);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(start);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(end);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(majorPoint);
                TreeNode.LastNode.LastNode.LastNode.Nodes.Add(minorPoint);
            }
        }
        

        /// <summary>
        ///     Процедура по записи свойств модели в файл.
        /// </summary>
        public static void CreateTemplateModelProperties()
        {
            var template = new StringBuilder();
            if (SketchInfos is null) return;
            foreach (var sketch in SketchInfos)
            {
                template.Append("Имя эскиза: " + sketch.SketchName + "\n");
                template.Append("Количество точек: " + sketch.PointCount + "\n");
                template.Append("Количество отрезков: " + sketch.LineCount + "\n");
                template.Append("Количество вспомогательных линий: " + sketch.LineTypes.Count(type => type == 4) +
                                "\n");
                template.Append("Количество дуг: " + sketch.ArcCount + "\n");
                template.Append("Количество эллипсов: " + sketch.EllipseCount + "\n");
                template.Append("Количество парабол: " + sketch.ParabolaCount + "\n");

                if (sketch.PointStatus)
                {
                    // ignored
                }

                if (sketch.LineStatus)
                {
                    var index = 0;
                    foreach (var line in sketch.LineCoordinates)
                        if (sketch.LineTypes[index] != 4)
                        {
                            template.Append("Отрезок: \n\t" + line.Replace("\n", "\n\t") + "\n\t");
                            template.Append("Длина: " + sketch.LineLengths[index++] + " мм\n");
                        }
                        else
                        {
                            index++;
                        }
                }

                if (sketch.ArcStatus)
                {
                    var index = 0;
                    foreach (var arc in sketch.ArcCoordinates)
                    {
                        template.Append("Дуга: \n\t" + arc.Replace("\n", "\n\t") + "\n\t");
                        template.Append("Радиус: " + sketch.ArcRadius[index++] + " мм\n");
                    }
                }

                if (sketch.EllipseStatus)
                    foreach (var ellipse in sketch.EllipseCoordinates)
                        template.Append("Эллипс: \n\t" + ellipse.Replace("\n", "\n\t") + "\n\t");

                if (sketch.ParabolaStatus)
                    foreach (var parabola in sketch.ParabolaCoordinates)
                        template.Append("Парабола: \n\t" + parabola.Replace("\n", "\n\t") + "\n\t");

                template.Append("Выдавливание: " + sketch.Deepth + " мм\n\n");
            }

            ModelProperties = template.ToString();
        }

        public static async void SavingModelPropertiesToAFile(string template)
        {
            const string path = @"..\..\Files/Свойства модели.txt";
            using var writer = new StreamWriter(path, false);
            await writer.WriteLineAsync(template);
        }
    }
}