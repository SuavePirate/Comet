using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMethodReturnValue.Global

namespace Comet.Graphics
{
    public class PathF : IDisposable
    {
        private readonly List<PointF> _points;
        private readonly List<PathOperation> _operations;
        
        private List<float> _arcAngles;
        private List<bool> _arcClockwise;
        
        private RectangleF? _cachedBounds;
        private object _nativePath;

        public PathF(PathF prototype, AffineTransformF transform = null) : this()
        {
            _operations.AddRange(prototype._operations);
            foreach (var point in prototype.Points)
            {
                var newPoint = point;
                
                if (transform != null)
                    newPoint = transform.Transform(point);

                _points.Add(newPoint);
            }
            if (prototype._arcAngles != null)
            {
                _arcAngles = new List<float>();
                _arcClockwise = new List<bool>();
                
                _arcAngles.AddRange(prototype._arcAngles);
                _arcClockwise.AddRange(prototype._arcClockwise);
            }
        }

        public PathF(PointF point) : this()
        {
            MoveTo(point);
        }

        public PathF(float x, float y) : this(new PointF(x, y))
        {
        }

        public PathF()
        {
            _points = new List<PointF>();
            _operations = new List<PathOperation>();
        }

        public bool Closed
        {
            get
            {
                if (_operations.Count > 0)
                    return _operations[_operations.Count - 1] == PathOperation.Close;

                return false;
            }
        }

        public PointF? FirstPoint
        {
            get
            {
                if (_points != null && _points.Count > 0)
                    return _points[0];

                return null;
            }
        }

        public IEnumerable<PathOperation> PathOperations
        {
            get
            {
                for (var i = 0; i < _operations.Count; i++)
                    yield return _operations[i];
            }
        }

        public IEnumerable<PointF> Points
        {
            get
            {
                for (var i = 0; i < _points.Count; i++)
                    yield return _points[i];
            }
        }

        public RectangleF Bounds
        {
            get
            {
                if (_cachedBounds != null)
                    return (RectangleF)_cachedBounds;

                _cachedBounds = CalculateBounds();
                
                /* var graphicsService = Device.GraphicsService;
                if (graphicsService != null)
                    _cachedBounds = graphicsService.GetPathBounds(this);
                else
                {
                    
                }*/

                return (RectangleF)_cachedBounds;
            }
        }

        private RectangleF CalculateBounds()
        {
            var xValues = new List<float>();
            var yValues = new List<float>();
            
            int pointIndex = 0;
            int arcAngleIndex = 0;
            int arcClockwiseIndex = 0;
            
            foreach (var operation in PathOperations)
            {
                if (operation == PathOperation.MoveTo)
                {
                    pointIndex++;
                }
                else if (operation == PathOperation.Line)
                {
                    var startPoint = _points[pointIndex-1];
                    var endPoint = _points[pointIndex++];
                    
                    xValues.Add(startPoint.X);
                    xValues.Add(endPoint.X);
                    yValues.Add(startPoint.Y);
                    yValues.Add(endPoint.Y);
                }
                else if (operation == PathOperation.Quad)
                {
                    var startPoint = _points[pointIndex-1];
                    var controlPoint = _points[pointIndex++];
                    var endPoint = _points[pointIndex++];

                    var bounds = GraphicsOperations.GetBoundsOfQuadraticCurve(startPoint, controlPoint, endPoint);
                    
                    xValues.Add(bounds.Left);
                    xValues.Add(bounds.Right);
                    yValues.Add(bounds.Top);
                    yValues.Add(bounds.Bottom);
                }
                else if (operation == PathOperation.Cubic)
                {
                    var startPoint = _points[pointIndex-1];
                    var controlPoint1 = _points[pointIndex++];
                    var controlPoint2 = _points[pointIndex++];
                    var endPoint = _points[pointIndex++];

                    var bounds = GraphicsOperations.GetBoundsOfCubicCurve(startPoint, controlPoint1, controlPoint2, endPoint);
                    
                    xValues.Add(bounds.Left);
                    xValues.Add(bounds.Right);
                    yValues.Add(bounds.Top);
                    yValues.Add(bounds.Bottom);
                }
                else if (operation == PathOperation.Arc)
                {
                    var topLeft = _points[pointIndex++];
                    var bottomRight = _points[pointIndex++];
                    float startAngle = GetArcAngle(arcAngleIndex++);
                    float endAngle = GetArcAngle(arcAngleIndex++);
                    var clockwise = IsArcClockwise(arcClockwiseIndex++);

                    var bounds = GraphicsOperations.GetBoundsOfArc(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y, startAngle, endAngle, clockwise);
                    
                    xValues.Add(bounds.Left);
                    xValues.Add(bounds.Right);
                    yValues.Add(bounds.Top);
                    yValues.Add(bounds.Bottom);
                }
            }
            
            var minX = xValues.Min();
            var minY = yValues.Min();
            var maxX = xValues.Max();
            var maxY = yValues.Max();

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public PointF? LastPoint
        {
            get
            {
                if (_points != null && _points.Count > 0)
                    return _points[_points.Count - 1];

                return null;
            }
        }
        
        public PointF this[int index]
        {
            get
            {
                if (index < 0 || index >= _points.Count)
                    throw new IndexOutOfRangeException();

                return _points[index];
            }
        }
        
        public int PointCount => _points.Count;

        public int OperationCount => _operations.Count;
        
        public PathOperation GetOperationType(int index)
        {
            return _operations[index];
        }

        public float GetArcAngle(int index)
        {
            if (_arcAngles != null && _arcAngles.Count > index)
                return _arcAngles[index];

            return 0;
        }

        public bool IsArcClockwise(int index)
        {
            if (_arcClockwise != null && _arcClockwise.Count > index)
                return _arcClockwise[index];

            return false;
        }
        
        public PathF MoveTo(float x, float y)
        {
            return MoveTo(new PointF(x, y));
        }

        public PathF MoveTo(PointF point)
        {
            _points.Add(point);
            _operations.Add(PathOperation.MoveTo);
            Invalidate();
            return this;
        }

        public void Close()
        {
            if (!Closed)
                _operations.Add(PathOperation.Close);

            Invalidate();
        }
        
        public PathF LineTo(float x, float y)
        {
            return LineTo(new PointF(x, y));
        }

        public PathF LineTo(PointF point)
        {
            if (_points.Count == 0)
            {
                _points.Add(point);
                _operations.Add(PathOperation.MoveTo);
            }
            else
            {
                _points.Add(point);
                _operations.Add(PathOperation.Line);
            }

            Invalidate();

            return this;
        }
        
        public PathF AddArc(float x1, float y1, float x2, float y2, float startAngle, float endAngle, bool clockwise)
        {
            return AddArc(new PointF(x1, y1), new PointF(x2, y2), startAngle, endAngle, clockwise);
        }

        public PathF AddArc(PointF topLeft, PointF bottomRight, float startAngle, float endAngle, bool clockwise)
        {
            if (_arcAngles == null)
            {
                _arcAngles = new List<float>();
                _arcClockwise = new List<bool>();
            }
            _points.Add(topLeft);
            _points.Add(bottomRight);
            _arcAngles.Add(startAngle);
            _arcAngles.Add(endAngle);
            _arcClockwise.Add(clockwise);
            _operations.Add(PathOperation.Arc);
            Invalidate();
            return this;
        }

        public PathF QuadTo(float cx, float cy, float x, float y)
        {
            return QuadTo(new PointF(cx, cy), new PointF(x, y));
        }

        public PathF QuadTo(PointF controlPoint, PointF point)
        {
            _points.Add(controlPoint);
            _points.Add(point);
            _operations.Add(PathOperation.Quad);
            Invalidate();
            return this;
        }

        public PathF CurveTo(float c1X, float c1Y, float c2X, float c2Y, float x, float y)
        {
            return CurveTo(new PointF(c1X, c1Y), new PointF(c2X, c2Y), new PointF(x, y));
        }

        public PathF CurveTo(PointF controlPoint1, PointF controlPoint2, PointF point)
        {
            _points.Add(controlPoint1);
            _points.Add(controlPoint2);
            _points.Add(point);
            _operations.Add(PathOperation.Cubic);
            Invalidate();
            return this;
        }
       
        public PathF Rotate(float angle)
        {
            var center = Bounds.Center;
            return Rotate(angle, center);
        }

        public PathF Rotate(float angle, PointF pivotPoint)
        {
            var path = new PathF();

            var index = 0;
            var arcIndex = 0;
            var clockwiseIndex = 0;

            foreach (var operation in _operations)
            {
                if (operation == PathOperation.MoveTo)
                {
                    var point = GetRotatedPoint(index++, pivotPoint, angle);
                    path.MoveTo(point);
                }
                else if (operation == PathOperation.Line)
                {
                    var point = GetRotatedPoint(index++, pivotPoint, angle);
                    path.LineTo(point.X, point.Y);
                }
                else if (operation == PathOperation.Quad)
                {
                    var controlPoint = GetRotatedPoint(index++, pivotPoint, angle);
                    var point = GetRotatedPoint(index++, pivotPoint, angle);
                    path.QuadTo(controlPoint.X, controlPoint.Y, point.X, point.Y);
                }
                else if (operation == PathOperation.Cubic)
                {
                    var controlPoint1 = GetRotatedPoint(index++, pivotPoint, angle);
                    var controlPoint2 = GetRotatedPoint(index++, pivotPoint, angle);
                    var point = GetRotatedPoint(index++, pivotPoint, angle);
                    path.CurveTo(controlPoint1.X, controlPoint1.Y, controlPoint2.X, controlPoint2.Y, point.X, point.Y);
                }
                else if (operation == PathOperation.Arc)
                {
                    var topLeft = GetRotatedPoint(index++, pivotPoint, angle);
                    var bottomRight = GetRotatedPoint(index++, pivotPoint, angle);
                    var startAngle = _arcAngles[arcIndex++];
                    var endAngle = _arcAngles[arcIndex++];
                    var clockwise = _arcClockwise[clockwiseIndex++];

                    path.AddArc(topLeft, bottomRight, startAngle, endAngle, clockwise);
                }
                else if (operation == PathOperation.Close)
                {
                    path.Close();
                }
            }

            return path;
        }

        private PointF GetRotatedPoint(int index, PointF center, float angleInDegrees)
        {
            var point = _points[index];
            return GraphicsOperations.RotatePoint(center, point, angleInDegrees);
        }

        public void AppendEllipse(RectangleF rect)
        {
            AppendEllipse(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public void AppendEllipse(float x, float y, float w, float h)
        {
            var minx = x;
            var miny = y;
            var maxx = minx + w;
            var maxy = miny + h;
            var midx = minx + (w / 2);
            var midy = miny + (h / 2);
            var offsetY = h / 2 * .55f;
            var offsetX = w / 2 * .55f;

            MoveTo(new PointF(minx, midy));
            CurveTo(new PointF(minx, midy - offsetY), new PointF(midx - offsetX, miny), new PointF(midx, miny));
            CurveTo(new PointF(midx + offsetX, miny), new PointF(maxx, midy - offsetY), new PointF(maxx, midy));
            CurveTo(new PointF(maxx, midy + offsetY), new PointF(midx + offsetX, maxy), new PointF(midx, maxy));
            CurveTo(new PointF(midx - offsetX, maxy), new PointF(minx, midy + offsetY), new PointF(minx, midy));
            Close();
        }

        public void AppendRectangle(RectangleF rect, bool includeLast = false)
        {
            AppendRectangle(rect.X, rect.Y, rect.Width, rect.Height, includeLast);
        }

        public void AppendRectangle(float x, float y, float w, float h, bool includeLast = false)
        {
            var minx = x;
            var miny = y;
            var maxx = minx + w;
            var maxy = miny + h;

            MoveTo(new PointF(minx, miny));
            LineTo(new PointF(maxx, miny));
            LineTo(new PointF(maxx, maxy));
            LineTo(new PointF(minx, maxy));

            if (includeLast)
                LineTo(new PointF(minx, miny));

            Close();
        }

        public void AppendRoundedRectangle(RectangleF rect, float cornerRadius, bool includeLast = false)
        {
            AppendRoundedRectangle(rect.X, rect.Y, rect.Width, rect.Height, cornerRadius, includeLast);
        }
        
        public void AppendRoundedRectangle(float x, float y, float w, float h, float cornerRadius, bool includeLast = false)
        {
            if (cornerRadius > h / 2)
                cornerRadius = h / 2;

            if (cornerRadius > w / 2)
                cornerRadius = w / 2;

            var minx = x;
            var miny = y;
            var maxx = minx + w;
            var maxy = miny + h;

            var handleOffset = cornerRadius * .55f;
            var cornerOffset = cornerRadius - handleOffset;

            MoveTo(new PointF(minx, miny + cornerRadius));
            CurveTo(new PointF(minx, miny + cornerOffset), new PointF(minx + cornerOffset, miny), new PointF(minx + cornerRadius, miny));
            LineTo(new PointF(maxx - cornerRadius, miny));
            CurveTo(new PointF(maxx - cornerOffset, miny), new PointF(maxx, miny + cornerOffset), new PointF(maxx, miny + cornerRadius));
            LineTo(new PointF(maxx, maxy - cornerRadius));
            CurveTo(new PointF(maxx, maxy - cornerOffset), new PointF(maxx - cornerOffset, maxy), new PointF(maxx - cornerRadius, maxy));
            LineTo(new PointF(minx + cornerRadius, maxy));
            CurveTo(new PointF(minx + cornerOffset, maxy), new PointF(minx, maxy - cornerOffset), new PointF(minx, maxy - cornerRadius));

            if (includeLast)
                LineTo(new PointF(minx, miny + cornerRadius));

            Close();
        }
        
        public object NativePath
        {
            get => _nativePath;
            set
            {
                if (_nativePath is IDisposable disposable)
                    disposable.Dispose();

                _nativePath = value;
            }
        }

        private void Invalidate()
        {
            _cachedBounds = null;
            ReleaseNative();
        }

        public void Dispose()
        {
            ReleaseNative();
        }

        private void ReleaseNative()
        {
            if (_nativePath is IDisposable disposable)
                disposable.Dispose();

            _nativePath = null;
        }

        public PathF Transform(AffineTransformF transform)
        {
            return new PathF(this, transform);
        }
    }
}
