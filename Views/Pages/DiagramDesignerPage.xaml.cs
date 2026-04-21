using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using KiteTodo.Models;
using KiteTodo.Services;
using Microsoft.Win32;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 流程设计页面：一个简单的流程图绘制工具。
/// 支持新建、打开、保存、添加节点、连线和拖拽。
/// </summary>
public partial class DiagramDesignerPage : Page
{
    private const int DefaultNodeZIndex = 10;
    private const double PasteOffsetStep = 24;
    private const int SavedFlowchartsPageSize = 8;
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PaletteDuplicateSuppressWindow = TimeSpan.FromMilliseconds(200);
    private static readonly string[] ConnectionLabelPresets = ["是", "否", "确定", "不确定"];

    private enum ExportImageScope
    {
        VisibleArea,
        EntireCanvas,
        ContentBounds
    }

    private sealed class ExportImageOptions
    {
        public required ExportImageScope Scope { get; init; }
        public required double Scale { get; init; }
    }

    private sealed class SnapResult
    {
        public double DeltaX { get; set; }
        public double DeltaY { get; set; }
        public double? VerticalGuideX { get; set; }
        public double? HorizontalGuideY { get; set; }
    }

    private sealed class ConnectionVisual
    {
        public required FlowchartConnection Connection { get; init; }
        public required Polyline Line { get; init; }
        public required Polyline HitArea { get; init; }
        public required Polygon Arrow { get; init; }
        public required Border LabelHost { get; init; }
        public required TextBlock LabelText { get; init; }
        public required Ellipse SourceHandle { get; init; }
        public required Ellipse TargetHandle { get; init; }
    }

    private sealed class NodeConnectorHandleTag
    {
        public required Guid NodeId { get; init; }
        public required FlowchartAnchorSide Side { get; init; }
        public double Coordinate { get; init; } = 0.5;
    }

    private sealed class ConnectionEndpointHandleTag
    {
        public required Guid ConnectionId { get; init; }
        public required bool IsSource { get; init; }
    }

    private sealed class ClipboardNodeSnapshot
    {
        public required FlowchartNode Node { get; init; }
    }

    private sealed class ClipboardConnectionSnapshot
    {
        public required Guid SourceNodeId { get; init; }
        public required Guid TargetNodeId { get; init; }
        public required string Label { get; init; }
        public required FlowchartConnectionStyle Style { get; init; }
        public bool? IsHorizontalFirst { get; init; }
        public FlowchartAnchorSide? SourceAnchorSide { get; init; }
        public FlowchartAnchorSide? TargetAnchorSide { get; init; }
        public double? SourceAnchorCoordinate { get; init; }
        public double? TargetAnchorCoordinate { get; init; }
        public required List<FlowchartConnectionControlPoint> ControlPoints { get; init; }
    }

    private sealed class SaveFlowchartRequest
    {
        public required string Name { get; init; }
        public required FlowchartStorageType StorageType { get; init; }
        public string? FilePath { get; init; }
    }

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly FlowchartStorageService _flowchartStorageService = new();
    private readonly Dictionary<Guid, FrameworkElement> _nodeViews = new();
    private readonly List<ConnectionVisual> _connectionViews = new();
    private readonly HashSet<Guid> _selectedNodeIds = new();
    private readonly ObservableCollection<FlowchartStorageItem> _savedFlowcharts = [];
    private readonly ObservableCollection<FlowchartStorageItem> _pagedSavedFlowcharts = [];
    private readonly DispatcherTimer _autoSaveTimer;

    private FlowchartDocument _document = new();
    private string? _currentFilePath;
    private int? _currentFlowchartStorageId;
    private FlowchartStorageType? _currentStorageType;
    private FrameworkElement? _draggingNode;
    private Guid? _draggingNodeId;
    private Point _dragMouseStart;
    private readonly Dictionary<Guid, Point> _dragNodeStartPositions = new();
    private double _zoomLevel = 1.0;
    private Guid? _connectingSourceNodeId;
    private FlowchartAnchorSide? _connectingSourceAnchorSide;
    private double? _connectingSourceAnchorCoordinate;
    private Ellipse? _connectingHandle;
    private Polyline? _previewConnectionLine;
    private Polygon? _previewConnectionArrow;
    private Guid? _editingNodeId;
    private Point _paletteDragStart;
    private string? _paletteDragType;
    private Button? _palettePressedButton;
    private bool _paletteSuppressMouseUpAdd;
    private bool _paletteSuppressClickAdd;
    private string? _lastPaletteAddKey;
    private DateTime _lastPaletteAddAt;
    private List<ClipboardNodeSnapshot> _clipboardNodes = [];
    private List<ClipboardConnectionSnapshot> _clipboardConnections = [];
    private int _pasteSequence;
    private bool _isCanvasSelecting;
    private bool _hasSelectionDragMoved;
    private bool _selectionAppendMode;
    private Point _canvasSelectionStart;
    private Guid? _selectedConnectionId;
    private int _savedFlowchartsPageIndex;
    private Guid? _draggingConnectionId;
    private bool _draggingConnectionSourceAnchor;
    private Ellipse? _draggingConnectionHandle;
    private bool _didMoveConnectionAnchor;
    private Point _canvasContextMenuPosition;
    private Guid? _resizingNodeId;
    private Point _resizeStartPoint;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private bool _hasPendingChanges;
    private bool _isAutoSaving;
    private bool _didMoveNodes;
    private bool _didResizeNode;
    private DateTime? _lastAutoSavedAt;

    public DiagramDesignerPage()
    {
        InitializeComponent();
        _autoSaveTimer = new DispatcherTimer { Interval = AutoSaveDelay };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
        SavedFlowchartsListBox.ItemsSource = _pagedSavedFlowcharts;
        DesignerCanvas.ContextMenu = CreateCanvasContextMenu();
        Loaded += (_, _) =>
        {
            if (_nodeViews.Count == 0 && _document.Nodes.Count == 0)
                ResetDocument();

            RefreshSavedFlowcharts();
            Focus();
        };
        Unloaded += (_, _) =>
        {
            _autoSaveTimer.Stop();
            if (_hasPendingChanges && _currentStorageType.HasValue)
                SaveCurrentDocument(isAutoSave: true);
        };
    }

    private void ResetDocument()
    {
        _document = new FlowchartDocument
        {
            Name = "未命名流程图"
        };
        _currentFilePath = null;
        _currentFlowchartStorageId = null;
        _currentStorageType = null;
        _selectedNodeIds.Clear();
        _selectedConnectionId = null;
        _editingNodeId = null;
        _pasteSequence = 0;
        _hasPendingChanges = false;
        _autoSaveTimer.Stop();
        RenderDocument();
        SavedFlowchartsListBox.SelectedItem = null;
    }

    private void RenderDocument()
    {
        DesignerCanvas.Children.Clear();
        _nodeViews.Clear();
        _connectionViews.Clear();

        foreach (var connection in _document.Connections)
        {
            CreateConnectionVisual(connection);
        }

        foreach (var node in _document.Nodes)
        {
            CreateNodeVisual(node);
        }

        UpdateSelectionVisuals();
        UpdateStatusBar();
    }

    private void CreateNodeVisual(FlowchartNode node)
    {
        var root = new Grid
        {
            Width = node.Width,
            Height = node.Height,
            Tag = node.Id,
            Cursor = Cursors.SizeAll,
            Background = Brushes.Transparent
        };

        FrameworkElement body = node.Type switch
        {
            FlowchartNodeType.StartEnd => CreateEllipseNode(node),
            FlowchartNodeType.Decision => CreateDecisionNode(node),
            FlowchartNodeType.Text => CreateTextNode(node),
            _ => CreateProcessNode(node)
        };

        root.Children.Add(body);
        Canvas.SetLeft(root, node.X);
        Canvas.SetTop(root, node.Y);
        Panel.SetZIndex(root, node.ZIndex);
        root.MouseLeftButtonDown += OnNodeMouseLeftButtonDown;
        root.MouseMove += OnNodeMouseMove;
        root.MouseLeftButtonUp += OnNodeMouseLeftButtonUp;
        root.PreviewMouseRightButtonDown += OnNodePreviewMouseRightButtonDown;
        root.ContextMenu = CreateNodeContextMenu();

        root.Children.Add(CreateConnectorHandle(node.Id, FlowchartAnchorSide.Top));
        root.Children.Add(CreateConnectorHandle(node.Id, FlowchartAnchorSide.Right));
        root.Children.Add(CreateConnectorHandle(node.Id, FlowchartAnchorSide.Bottom));
        root.Children.Add(CreateConnectorHandle(node.Id, FlowchartAnchorSide.Left));

        var resizeHandle = new Border
        {
            Width = 12,
            Height = 12,
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -8, -8),
            Cursor = Cursors.SizeNWSE,
            Tag = node.Id,
            Uid = "ResizeHandle",
            Visibility = Visibility.Collapsed
        };
        resizeHandle.MouseLeftButtonDown += OnResizeHandleMouseLeftButtonDown;
        resizeHandle.MouseMove += OnResizeHandleMouseMove;
        resizeHandle.MouseLeftButtonUp += OnResizeHandleMouseLeftButtonUp;
        root.Children.Add(resizeHandle);

        _nodeViews[node.Id] = root;
        DesignerCanvas.Children.Add(root);
    }

    private Ellipse CreateConnectorHandle(Guid nodeId, FlowchartAnchorSide side)
    {
        var handle = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Cursor = Cursors.Cross,
            Tag = new NodeConnectorHandleTag
            {
                NodeId = nodeId,
                Side = side
            },
            Uid = "ConnectorHandle",
            Visibility = Visibility.Collapsed
        };

        switch (side)
        {
            case FlowchartAnchorSide.Top:
                handle.HorizontalAlignment = HorizontalAlignment.Center;
                handle.VerticalAlignment = VerticalAlignment.Top;
                handle.Margin = new Thickness(0, -8, 0, 0);
                break;
            case FlowchartAnchorSide.Right:
                handle.HorizontalAlignment = HorizontalAlignment.Right;
                handle.VerticalAlignment = VerticalAlignment.Center;
                handle.Margin = new Thickness(0, 0, -8, 0);
                break;
            case FlowchartAnchorSide.Bottom:
                handle.HorizontalAlignment = HorizontalAlignment.Center;
                handle.VerticalAlignment = VerticalAlignment.Bottom;
                handle.Margin = new Thickness(0, 0, 0, -8);
                break;
            default:
                handle.HorizontalAlignment = HorizontalAlignment.Left;
                handle.VerticalAlignment = VerticalAlignment.Center;
                handle.Margin = new Thickness(-8, 0, 0, 0);
                break;
        }

        handle.MouseLeftButtonDown += OnConnectorHandleMouseLeftButtonDown;
        handle.MouseMove += OnConnectorHandleMouseMove;
        handle.MouseLeftButtonUp += OnConnectorHandleMouseLeftButtonUp;
        return handle;
    }

    private FrameworkElement CreateProcessNode(FlowchartNode node)
    {
        return new Border
        {
            Width = node.Width,
            Height = node.Height,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            BorderThickness = new Thickness(2),
            Child = CreateNodeContent(node)
        };
    }

    private FrameworkElement CreateEllipseNode(FlowchartNode node)
    {
        var grid = new Grid { Width = node.Width, Height = node.Height };
        grid.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromRgb(240, 249, 255)),
            Stroke = new SolidColorBrush(Color.FromRgb(14, 116, 144)),
            StrokeThickness = 2
        });
        grid.Children.Add(CreateNodeContent(node));
        return grid;
    }

    private FrameworkElement CreateDecisionNode(FlowchartNode node)
    {
        var grid = new Grid { Width = node.Width, Height = node.Height };
        grid.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(node.Width / 2, 0),
                new(node.Width, node.Height / 2),
                new(node.Width / 2, node.Height),
                new(0, node.Height / 2)
            },
            Fill = new SolidColorBrush(Color.FromRgb(255, 251, 235)),
            Stroke = new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            StrokeThickness = 2
        });
        grid.Children.Add(CreateNodeContent(node));
        return grid;
    }

    private FrameworkElement CreateTextNode(FlowchartNode node)
    {
        var grid = new Grid
        {
            Width = node.Width,
            Height = node.Height
        };
        grid.Children.Add(new Rectangle
        {
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            StrokeThickness = 1.5,
            StrokeDashArray = [4, 2],
            RadiusX = 4,
            RadiusY = 4
        });
        grid.Children.Add(CreateNodeContent(node));
        return grid;
    }

    private FrameworkElement CreateNodeContent(FlowchartNode node)
    {
        if (_editingNodeId == node.Id)
        {
            var textBox = new TextBox
            {
                Text = node.Text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(12, 8, 12, 8),
                MinWidth = Math.Max(40, node.Width - 24),
                Tag = node.Id,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                BorderThickness = new Thickness(1.5)
            };
            textBox.Loaded += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };
            textBox.KeyDown += OnInlineNodeEditorKeyDown;
            textBox.LostKeyboardFocus += OnInlineNodeEditorLostFocus;
            return textBox;
        }

        return CreateNodeText(node.Text);
    }

    private static TextBlock CreateNodeText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(12, 8, 12, 8),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
        };
    }

    private void CreateConnectionVisual(FlowchartConnection connection)
    {
        var line = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        var hitArea = new Polyline
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 14,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Cursor = Cursors.Hand
        };
        hitArea.MouseLeftButtonDown += OnConnectionMouseLeftButtonDown;
        hitArea.PreviewMouseRightButtonDown += OnConnectionPreviewMouseRightButtonDown;
        hitArea.ContextMenu = CreateConnectionContextMenu(connection.Id);
        var arrow = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            IsHitTestVisible = false
        };
        var labelText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 140
        };
        var labelHost = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            Child = labelText,
            Cursor = Cursors.IBeam,
            Visibility = Visibility.Collapsed,
            Tag = connection.Id
        };
        labelHost.MouseLeftButtonDown += OnConnectionLabelMouseLeftButtonDown;
        labelHost.PreviewMouseRightButtonDown += OnConnectionLabelPreviewMouseRightButtonDown;
        labelHost.ContextMenu = CreateConnectionContextMenu(connection.Id);
        var sourceHandle = CreateConnectionEndpointHandle(connection.Id, true);
        var targetHandle = CreateConnectionEndpointHandle(connection.Id, false);
        Panel.SetZIndex(hitArea, 2);
        Panel.SetZIndex(line, 1);
        Panel.SetZIndex(arrow, 1);
        Panel.SetZIndex(labelHost, 4);
        Panel.SetZIndex(sourceHandle, 3);
        Panel.SetZIndex(targetHandle, 3);
        DesignerCanvas.Children.Add(hitArea);
        DesignerCanvas.Children.Add(line);
        DesignerCanvas.Children.Add(arrow);
        DesignerCanvas.Children.Add(labelHost);
        DesignerCanvas.Children.Add(sourceHandle);
        DesignerCanvas.Children.Add(targetHandle);

        var visual = new ConnectionVisual
        {
            Connection = connection,
            Line = line,
            HitArea = hitArea,
            Arrow = arrow,
            LabelHost = labelHost,
            LabelText = labelText,
            SourceHandle = sourceHandle,
            TargetHandle = targetHandle
        };
        _connectionViews.Add(visual);
        UpdateConnectionVisual(visual);
    }

    private Ellipse CreateConnectionEndpointHandle(Guid connectionId, bool isSource)
    {
        var handle = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed,
            Tag = new ConnectionEndpointHandleTag
            {
                ConnectionId = connectionId,
                IsSource = isSource
            }
        };
        handle.MouseLeftButtonDown += OnConnectionEndpointHandleMouseLeftButtonDown;
        handle.MouseMove += OnConnectionEndpointHandleMouseMove;
        handle.MouseLeftButtonUp += OnConnectionEndpointHandleMouseLeftButtonUp;
        return handle;
    }

    private void UpdateConnectionVisual(ConnectionVisual visual)
    {
        var source = _document.Nodes.FirstOrDefault(n => n.Id == visual.Connection.SourceNodeId);
        var target = _document.Nodes.FirstOrDefault(n => n.Id == visual.Connection.TargetNodeId);
        if (source == null || target == null)
            return;

        var sourceCenter = GetNodeCenter(source);
        var targetCenter = GetNodeCenter(target);
    var start = GetConnectionAnchorPoint(source, visual.Connection.SourceAnchorSide, visual.Connection.SourceAnchorCoordinate, targetCenter);
    var end = GetConnectionAnchorPoint(target, visual.Connection.TargetAnchorSide, visual.Connection.TargetAnchorCoordinate, sourceCenter);

        var points = BuildConnectionPath(visual.Connection, start, end);
        visual.Line.Points = new PointCollection(points);
        visual.HitArea.Points = new PointCollection(points);
        SetConnectionHandlePosition(visual.SourceHandle, start);
        SetConnectionHandlePosition(visual.TargetHandle, end);
        UpdateConnectionLabelVisual(visual, points);

        var arrowBase = points.Count >= 2 ? points[^2] : start;
        var angle = Math.Atan2(end.Y - arrowBase.Y, end.X - arrowBase.X);
        const double arrowLength = 18;
        const double arrowWidth = 8;
        var p1 = end;
        var p2 = new Point(
            end.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Sin(angle),
            end.Y - arrowLength * Math.Sin(angle) - arrowWidth * Math.Cos(angle));
        var p3 = new Point(
            end.X - arrowLength * Math.Cos(angle) - arrowWidth * Math.Sin(angle),
            end.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Cos(angle));
        visual.Arrow.Points = new PointCollection { p1, p2, p3 };
    }

    private static void SetConnectionHandlePosition(FrameworkElement handle, Point center)
    {
        Canvas.SetLeft(handle, center.X - handle.Width / 2);
        Canvas.SetTop(handle, center.Y - handle.Height / 2);
    }

    private void UpdateConnectionLabelVisual(ConnectionVisual visual, IReadOnlyList<Point> points)
    {
        var label = visual.Connection.Label?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(label) || points.Count < 2)
        {
            visual.LabelHost.Visibility = Visibility.Collapsed;
            return;
        }

        visual.LabelText.Text = label;
        visual.LabelHost.Visibility = Visibility.Visible;
        visual.LabelHost.Measure(new Size(160, double.PositiveInfinity));
        var labelPosition = GetPolylineMidpoint(points);
        var desiredSize = visual.LabelHost.DesiredSize;
        Canvas.SetLeft(visual.LabelHost, labelPosition.X - desiredSize.Width / 2);
        Canvas.SetTop(visual.LabelHost, labelPosition.Y - desiredSize.Height / 2);
    }

    private static Point GetPolylineMidpoint(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
            return new Point();

        if (points.Count == 1)
            return points[0];

        var totalLength = 0d;
        for (var index = 1; index < points.Count; index++)
            totalLength += (points[index] - points[index - 1]).Length;

        if (totalLength < 0.001)
            return points[0];

        var halfLength = totalLength / 2;
        var traversed = 0d;
        for (var index = 1; index < points.Count; index++)
        {
            var start = points[index - 1];
            var end = points[index];
            var segmentVector = end - start;
            var segmentLength = segmentVector.Length;
            if (segmentLength < 0.001)
                continue;

            if (traversed + segmentLength >= halfLength)
            {
                var ratio = (halfLength - traversed) / segmentLength;
                return new Point(start.X + segmentVector.X * ratio, start.Y + segmentVector.Y * ratio);
            }

            traversed += segmentLength;
        }

        return points[^1];
    }

    private static List<Point> BuildConnectionPath(FlowchartConnection? connection, Point start, Point end)
    {
        if (connection?.Style == FlowchartConnectionStyle.Straight)
            return [start, end];

        return BuildOrthogonalPath(connection, start, end);
    }

    private static List<Point> BuildOrthogonalPath(FlowchartConnection? connection, Point start, Point end)
    {
        var points = new List<Point> { start };
        var horizontalFirst = IsHorizontalFirst(connection, start, end);
        if (horizontalFirst)
        {
            var midX = start.X + (end.X - start.X) / 2;
            points.Add(new Point(midX, start.Y));
            points.Add(new Point(midX, end.Y));
        }
        else
        {
            var midY = start.Y + (end.Y - start.Y) / 2;
            points.Add(new Point(start.X, midY));
            points.Add(new Point(end.X, midY));
        }
        points.Add(end);
        return NormalizePolylinePoints(points
            .Where((point, index) => index == 0 || point != points[index - 1])
            .ToList());
    }

    private static bool IsHorizontalFirst(FlowchartConnection? connection, Point start, Point end)
    {
        return connection?.IsHorizontalFirst ?? (Math.Abs(end.X - start.X) > Math.Abs(end.Y - start.Y));
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.001;
    }

    private static List<Point> NormalizePolylinePoints(List<Point> points)
    {
        if (points.Count <= 2)
            return points;

        var normalized = new List<Point> { points[0] };
        for (var index = 1; index < points.Count - 1; index++)
        {
            var previous = normalized[^1];
            var current = points[index];
            var next = points[index + 1];

            var isVertical = AreClose(previous.X, current.X) && AreClose(current.X, next.X);
            var isHorizontal = AreClose(previous.Y, current.Y) && AreClose(current.Y, next.Y);
            if (isVertical || isHorizontal)
                continue;

            normalized.Add(current);
        }

        normalized.Add(points[^1]);
        return normalized;
    }

    private static void ResetConnectionRoutingState(FlowchartConnection connection)
    {
        connection.ControlPoints.Clear();
        connection.HandleSegmentIndex = null;
        connection.ManualHandleSecondaryCoordinate = null;
        connection.ManualMiddleCoordinate = null;
    }

    private static Point GetNodeCenter(FlowchartNode node)
    {
        return new Point(node.X + node.Width / 2, node.Y + node.Height / 2);
    }

    private static Point GetNodeAnchorPoint(FlowchartNode node, Point toward)
    {
        var center = GetNodeCenter(node);
        var dx = toward.X - center.X;
        var dy = toward.Y - center.Y;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            return center;

        return node.Type switch
        {
            FlowchartNodeType.StartEnd => GetEllipseAnchor(node, center, dx, dy),
            FlowchartNodeType.Decision => GetDiamondAnchor(node, center, dx, dy),
            _ => GetRectangleAnchor(node, center, dx, dy)
        };
    }

    private static Point GetConnectionAnchorPoint(FlowchartNode node, FlowchartAnchorSide? side, double? coordinate, Point toward)
    {
        return side.HasValue
            ? GetNodeAnchorPoint(node, side.Value, NormalizeAnchorCoordinate(coordinate))
            : GetNodeAnchorPoint(node, toward);
    }

    private static Point GetNodeAnchorPoint(FlowchartNode node, FlowchartAnchorSide side, double coordinate)
    {
        return node.Type switch
        {
            FlowchartNodeType.StartEnd => GetEllipseAnchor(node, side, coordinate),
            FlowchartNodeType.Decision => GetDiamondAnchor(node, side, coordinate),
            _ => GetRectangleAnchor(node, side, coordinate)
        };
    }

    private static double NormalizeAnchorCoordinate(double? coordinate)
    {
        return Clamp(coordinate ?? 0.5, 0, 1);
    }

    private static (FlowchartAnchorSide Side, double Coordinate, Point Point) GetNearestAnchorPlacement(FlowchartNode node, Point position)
    {
        var bestSide = FlowchartAnchorSide.Right;
        var bestCoordinate = 0.5;
        var bestPoint = GetNodeAnchorPoint(node, bestSide, bestCoordinate);
        var bestDistance = (bestPoint - position).LengthSquared;

        foreach (var side in new[] { FlowchartAnchorSide.Left, FlowchartAnchorSide.Top, FlowchartAnchorSide.Right, FlowchartAnchorSide.Bottom })
        {
            var coordinate = GetAnchorCoordinateForPosition(node, side, position);
            var anchorPoint = GetNodeAnchorPoint(node, side, coordinate);
            var distance = (anchorPoint - position).LengthSquared;
            if (distance >= bestDistance)
                continue;

            bestSide = side;
            bestCoordinate = coordinate;
            bestPoint = anchorPoint;
            bestDistance = distance;
        }

        return (bestSide, bestCoordinate, bestPoint);
    }

    private static double GetAnchorCoordinateForPosition(FlowchartNode node, FlowchartAnchorSide side, Point position)
    {
        return side is FlowchartAnchorSide.Left or FlowchartAnchorSide.Right
            ? NormalizeAnchorCoordinate(node.Height < 0.001 ? 0.5 : (position.Y - node.Y) / node.Height)
            : NormalizeAnchorCoordinate(node.Width < 0.001 ? 0.5 : (position.X - node.X) / node.Width);
    }

    private static Point GetRectangleAnchor(FlowchartNode node, Point center, double dx, double dy)
    {
        var halfWidth = node.Width / 2;
        var halfHeight = node.Height / 2;
        var scaleX = Math.Abs(dx) < 0.001 ? double.PositiveInfinity : halfWidth / Math.Abs(dx);
        var scaleY = Math.Abs(dy) < 0.001 ? double.PositiveInfinity : halfHeight / Math.Abs(dy);
        var scale = Math.Min(scaleX, scaleY);
        return new Point(center.X + dx * scale, center.Y + dy * scale);
    }

    private static Point GetRectangleAnchor(FlowchartNode node, FlowchartAnchorSide side, double coordinate)
    {
        var ratio = NormalizeAnchorCoordinate(coordinate);
        return side switch
        {
            FlowchartAnchorSide.Left => new Point(node.X, node.Y + node.Height * ratio),
            FlowchartAnchorSide.Top => new Point(node.X + node.Width * ratio, node.Y),
            FlowchartAnchorSide.Right => new Point(node.X + node.Width, node.Y + node.Height * ratio),
            _ => new Point(node.X + node.Width * ratio, node.Y + node.Height)
        };
    }

    private static Point GetEllipseAnchor(FlowchartNode node, Point center, double dx, double dy)
    {
        var radiusX = node.Width / 2;
        var radiusY = node.Height / 2;
        var denominator = Math.Sqrt((dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY));
        return denominator < 0.001
            ? center
            : new Point(center.X + dx / denominator, center.Y + dy / denominator);
    }

    private static Point GetEllipseAnchor(FlowchartNode node, FlowchartAnchorSide side, double coordinate)
    {
        var center = GetNodeCenter(node);
        var radiusX = node.Width / 2;
        var radiusY = node.Height / 2;
        var ratio = NormalizeAnchorCoordinate(coordinate);

        if (side is FlowchartAnchorSide.Top or FlowchartAnchorSide.Bottom)
        {
            var offsetX = (ratio - 0.5) * 2 * radiusX;
            var yFactor = Math.Sqrt(Math.Max(0, 1 - (offsetX * offsetX) / (radiusX * radiusX)));
            var offsetY = radiusY * yFactor * (side == FlowchartAnchorSide.Top ? -1 : 1);
            return new Point(center.X + offsetX, center.Y + offsetY);
        }

        var offsetYVertical = (ratio - 0.5) * 2 * radiusY;
        var xFactor = Math.Sqrt(Math.Max(0, 1 - (offsetYVertical * offsetYVertical) / (radiusY * radiusY)));
        var offsetXVertical = radiusX * xFactor * (side == FlowchartAnchorSide.Left ? -1 : 1);
        return new Point(center.X + offsetXVertical, center.Y + offsetYVertical);
    }

    private static Point GetDiamondAnchor(FlowchartNode node, Point center, double dx, double dy)
    {
        var halfWidth = node.Width / 2;
        var halfHeight = node.Height / 2;
        var denominator = (Math.Abs(dx) / halfWidth) + (Math.Abs(dy) / halfHeight);
        return denominator < 0.001
            ? center
            : new Point(center.X + dx / denominator, center.Y + dy / denominator);
    }

    private static Point GetDiamondAnchor(FlowchartNode node, FlowchartAnchorSide side, double coordinate)
    {
        var center = GetNodeCenter(node);
        var halfWidth = node.Width / 2;
        var halfHeight = node.Height / 2;
        var ratio = NormalizeAnchorCoordinate(coordinate);

        if (side is FlowchartAnchorSide.Top or FlowchartAnchorSide.Bottom)
        {
            var offsetX = (ratio - 0.5) * 2 * halfWidth;
            var offsetY = halfHeight * (1 - Math.Abs(offsetX) / halfWidth) * (side == FlowchartAnchorSide.Top ? -1 : 1);
            return new Point(center.X + offsetX, center.Y + offsetY);
        }

        var offsetYVertical = (ratio - 0.5) * 2 * halfHeight;
        var offsetXVertical = halfWidth * (1 - Math.Abs(offsetYVertical) / halfHeight) * (side == FlowchartAnchorSide.Left ? -1 : 1);
        return new Point(center.X + offsetXVertical, center.Y + offsetYVertical);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var pair in _nodeViews)
        {
            pair.Value.Effect = _selectedNodeIds.Contains(pair.Key)
                ? new DropShadowEffect
                {
                    Color = Color.FromRgb(37, 99, 235),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.7
                }
                : null;

            if (pair.Value is Grid grid)
            {
                foreach (var ellipse in grid.Children.OfType<Ellipse>().Where(child => child.Uid == "ConnectorHandle"))
                {
                    ellipse.Visibility = _selectedNodeIds.Contains(pair.Key) ? Visibility.Visible : Visibility.Collapsed;
                }

                foreach (var border in grid.Children.OfType<Border>().Where(child => Equals(child.Tag, pair.Key) && child.Uid == "ResizeHandle"))
                {
                    border.Visibility = _selectedNodeIds.Contains(pair.Key) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        foreach (var connectionView in _connectionViews)
        {
            var isSelected = _selectedConnectionId == connectionView.Connection.Id;
            connectionView.Line.Stroke = isSelected
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                : new SolidColorBrush(Color.FromRgb(71, 85, 105));
            connectionView.Arrow.Fill = isSelected
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                : new SolidColorBrush(Color.FromRgb(71, 85, 105));
            connectionView.LabelHost.BorderBrush = isSelected
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                : new SolidColorBrush(Color.FromRgb(203, 213, 225));
            connectionView.LabelHost.Background = isSelected
                ? new SolidColorBrush(Color.FromArgb(245, 219, 234, 254))
                : new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            connectionView.SourceHandle.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            connectionView.TargetHandle.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var storageText = _currentStorageType switch
        {
            FlowchartStorageType.Database => "数据库",
            FlowchartStorageType.File when !string.IsNullOrWhiteSpace(_currentFilePath) => System.IO.Path.GetFileName(_currentFilePath),
            _ => "未保存"
        };
        var dirtyText = _hasPendingChanges ? "  • 未保存更改" : string.Empty;
        var autoSaveText = _lastAutoSavedAt.HasValue ? $"  • 自动保存 {_lastAutoSavedAt.Value:HH:mm:ss}" : string.Empty;
        DocumentNameText.Text = $"{_document.Name}    {storageText}{dirtyText}{autoSaveText}";
        NodeCountText.Text = $"图元: {_document.Nodes.Count}  连线: {_document.Connections.Count}";
        SelectionInfoText.Text = $"已选中: {_selectedNodeIds.Count}    缩放: {(int)Math.Round(_zoomLevel * 100)}%";
    }

    private void MarkDocumentDirty(bool immediateAutoSave = false)
    {
        _hasPendingChanges = true;
        UpdateStatusBar();

        if (!_currentStorageType.HasValue || _isAutoSaving)
            return;

        _autoSaveTimer.Stop();
        _autoSaveTimer.Interval = immediateAutoSave ? TimeSpan.FromMilliseconds(250) : AutoSaveDelay;
        _autoSaveTimer.Start();
    }

    private void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();
        if (!_hasPendingChanges || !_currentStorageType.HasValue || _isAutoSaving)
            return;

        SaveCurrentDocument(isAutoSave: true);
    }

    private void RefreshSavedFlowcharts()
    {
        _savedFlowcharts.Clear();
        foreach (var item in _flowchartStorageService.GetAll())
            _savedFlowcharts.Add(item);

        if (_currentFlowchartStorageId.HasValue)
            MoveSavedFlowchartsPageToItem(_currentFlowchartStorageId.Value);

        RefreshPagedSavedFlowcharts();
    }

    private void RefreshPagedSavedFlowcharts()
    {
        var pageCount = GetSavedFlowchartsPageCount();
        _savedFlowchartsPageIndex = Math.Clamp(_savedFlowchartsPageIndex, 0, Math.Max(0, pageCount - 1));

        _pagedSavedFlowcharts.Clear();
        foreach (var item in _savedFlowcharts.Skip(_savedFlowchartsPageIndex * SavedFlowchartsPageSize).Take(SavedFlowchartsPageSize))
            _pagedSavedFlowcharts.Add(item);

        SavedFlowchartsPageInfoText.Text = $"第 {_savedFlowchartsPageIndex + 1} / {pageCount} 页";
        SavedFlowchartsPrevPageButton.IsEnabled = _savedFlowchartsPageIndex > 0;
        SavedFlowchartsNextPageButton.IsEnabled = _savedFlowchartsPageIndex < pageCount - 1;

        if (_currentFlowchartStorageId.HasValue)
            SavedFlowchartsListBox.SelectedItem = _pagedSavedFlowcharts.FirstOrDefault(item => item.Id == _currentFlowchartStorageId.Value);
        else
            SavedFlowchartsListBox.SelectedItem = null;
    }

    private int GetSavedFlowchartsPageCount()
    {
        return Math.Max(1, (int)Math.Ceiling(_savedFlowcharts.Count / (double)SavedFlowchartsPageSize));
    }

    private void MoveSavedFlowchartsPageToItem(int itemId)
    {
        var index = _savedFlowcharts
            .Select((item, position) => new { item, position })
            .FirstOrDefault(entry => entry.item.Id == itemId)?.position;

        if (index.HasValue)
            _savedFlowchartsPageIndex = index.Value / SavedFlowchartsPageSize;
        else
            _savedFlowchartsPageIndex = Math.Clamp(_savedFlowchartsPageIndex, 0, Math.Max(0, GetSavedFlowchartsPageCount() - 1));
    }

    private void OnSavedFlowchartsPrevPageClick(object sender, RoutedEventArgs e)
    {
        if (_savedFlowchartsPageIndex <= 0)
            return;

        _savedFlowchartsPageIndex--;
        RefreshPagedSavedFlowcharts();
    }

    private void OnSavedFlowchartsNextPageClick(object sender, RoutedEventArgs e)
    {
        if (_savedFlowchartsPageIndex >= GetSavedFlowchartsPageCount() - 1)
            return;

        _savedFlowchartsPageIndex++;
        RefreshPagedSavedFlowcharts();
    }

    private ContextMenu CreateNodeContextMenu()
    {
        var menu = new ContextMenu();

        var cutItem = new MenuItem { Header = "剪切" };
        cutItem.Click += OnCutSelected;

        var copyItem = new MenuItem { Header = "复制" };
        copyItem.Click += OnCopySelected;

        var pasteItem = new MenuItem { Header = "粘贴" };
        pasteItem.Click += OnPasteClipboard;

        var deleteItem = new MenuItem { Header = "删除" };
        deleteItem.Click += OnDeleteSelected;

        var bringToFrontItem = new MenuItem { Header = "置前" };
        bringToFrontItem.Click += OnBringToFrontSelected;

        var sendToBackItem = new MenuItem { Header = "置后" };
        sendToBackItem.Click += OnSendToBackSelected;

        menu.Items.Add(cutItem);
        menu.Items.Add(copyItem);
        menu.Items.Add(pasteItem);
        menu.Items.Add(deleteItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(bringToFrontItem);
        menu.Items.Add(sendToBackItem);
        menu.Opened += (_, _) => pasteItem.IsEnabled = _clipboardNodes.Count > 0;
        return menu;
    }

    private ContextMenu CreateCanvasContextMenu()
    {
        var menu = new ContextMenu();
        var pasteItem = new MenuItem { Header = "粘贴" };
        pasteItem.Click += OnPasteAtCanvasLocation;
        var copyAsImageItem = new MenuItem { Header = "复制为图片" };
        copyAsImageItem.Click += OnCopyCanvasAsImage;
        var exportPngItem = new MenuItem { Header = "导出 PNG" };
        exportPngItem.Click += OnExportCanvasAsPng;
        var selectAllItem = new MenuItem { Header = "全选" };
        selectAllItem.Click += OnSelectAll;
        menu.Items.Add(pasteItem);
        menu.Items.Add(copyAsImageItem);
        menu.Items.Add(exportPngItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(selectAllItem);
        menu.Opened += (_, _) => pasteItem.IsEnabled = _clipboardNodes.Count > 0;
        return menu;
    }

    private ContextMenu CreateConnectionContextMenu(Guid connectionId)
    {
        var menu = new ContextMenu();
        var editTextItem = new MenuItem { Header = "编辑线段文字", Tag = connectionId };
        editTextItem.Click += OnEditConnectionLabelClick;
        var presetMenu = new MenuItem { Header = "预设标签" };
        foreach (var preset in ConnectionLabelPresets)
        {
            var presetItem = new MenuItem { Header = preset, Tag = (connectionId, preset) };
            presetItem.Click += OnApplyConnectionPresetLabelClick;
            presetMenu.Items.Add(presetItem);
        }
        var clearTextItem = new MenuItem { Header = "清空线段文字", Tag = connectionId };
        clearTextItem.Click += OnClearConnectionLabelClick;
        var toggleStyleItem = new MenuItem { Header = "切换直线/折线", Tag = connectionId };
        toggleStyleItem.Click += OnToggleConnectionStyleClick;
        var deleteItem = new MenuItem { Header = "删除", Tag = connectionId };
        deleteItem.Click += OnDeleteConnectionClick;
        menu.Items.Add(editTextItem);
        menu.Items.Add(presetMenu);
        menu.Items.Add(clearTextItem);
        menu.Items.Add(toggleStyleItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(deleteItem);
        menu.Opened += (_, _) =>
        {
            var connection = _document.Connections.FirstOrDefault(item => item.Id == connectionId);
            clearTextItem.IsEnabled = connection != null && !string.IsNullOrWhiteSpace(connection.Label);
            toggleStyleItem.Header = connection?.Style == FlowchartConnectionStyle.Straight ? "切换为折线" : "切换为直线";
        };
        return menu;
    }

    private void OnConnectionLabelMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: Guid connectionId })
            return;

        SelectConnection(connectionId);
        if (e.ClickCount >= 2)
        {
            EditConnectionLabel(connectionId);
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }

    private void OnConnectionLabelPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: Guid connectionId })
            SelectConnection(connectionId);
    }

    private void SelectConnection(Guid connectionId)
    {
        _selectedNodeIds.Clear();
        _selectedConnectionId = connectionId;
        _editingNodeId = null;
        UpdateSelectionVisuals();
        Focus();
    }

    private void SelectNode(Guid nodeId, bool append)
    {
        _selectedConnectionId = null;

        if (!append)
            _selectedNodeIds.Clear();

        if (append && _selectedNodeIds.Contains(nodeId))
            _selectedNodeIds.Remove(nodeId);
        else
            _selectedNodeIds.Add(nodeId);

        if (_selectedNodeIds.Count == 1)
        {
            if (_editingNodeId.HasValue && !_selectedNodeIds.Contains(_editingNodeId.Value))
                _editingNodeId = null;
        }
        else
        {
            _editingNodeId = null;
        }

        UpdateSelectionVisuals();
    }

    private void SelectAllNodes()
    {
        _selectedNodeIds.Clear();
        foreach (var node in _document.Nodes)
            _selectedNodeIds.Add(node.Id);

        _editingNodeId = null;
        UpdateSelectionVisuals();
        Focus();
    }

    private void OnNodeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid nodeId)
            return;

        if (e.ClickCount == 2)
        {
            SelectNode(nodeId, false);
            BeginNodeTextEdit(nodeId);
            e.Handled = true;
            return;
        }

        var isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var wasSelected = _selectedNodeIds.Contains(nodeId);

        if (isCtrlPressed)
        {
            SelectNode(nodeId, true);
            if (!_selectedNodeIds.Contains(nodeId))
            {
                _draggingNode = null;
                _draggingNodeId = null;
                _dragNodeStartPositions.Clear();
                e.Handled = true;
                return;
            }
        }
        else if (!wasSelected)
        {
            SelectNode(nodeId, false);
        }

        _draggingNode = element;
        _draggingNodeId = nodeId;
        _didMoveNodes = false;
        _dragMouseStart = e.GetPosition(DesignerCanvas);
        _dragNodeStartPositions.Clear();
        foreach (var selectedNodeId in _selectedNodeIds)
        {
            var selectedNode = _document.Nodes.First(n => n.Id == selectedNodeId);
            _dragNodeStartPositions[selectedNodeId] = new Point(selectedNode.X, selectedNode.Y);
        }
        element.CaptureMouse();
        Focus();
        e.Handled = true;
    }

    private void OnNodeMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizingNodeId.HasValue)
            return;

        if (_draggingNode is not FrameworkElement element || !element.IsMouseCaptured || _draggingNodeId == null)
            return;

        var current = e.GetPosition(DesignerCanvas);
        var delta = current - _dragMouseStart;
        if (!_didMoveNodes && Math.Abs(delta.X) < 0.5 && Math.Abs(delta.Y) < 0.5)
            return;

        _didMoveNodes = true;
        var snap = BuildSnapResult(_draggingNodeId.Value, delta);
        var snappedDeltaX = delta.X + snap.DeltaX;
        var snappedDeltaY = delta.Y + snap.DeltaY;

        foreach (var selectedNodeId in _selectedNodeIds)
        {
            var node = _document.Nodes.First(n => n.Id == selectedNodeId);
            var startPosition = _dragNodeStartPositions[selectedNodeId];
            node.X = Math.Max(0, startPosition.X + snappedDeltaX);
            node.Y = Math.Max(0, startPosition.Y + snappedDeltaY);

            var nodeView = _nodeViews[selectedNodeId];
            Canvas.SetLeft(nodeView, node.X);
            Canvas.SetTop(nodeView, node.Y);
        }

        UpdateGuideLines(snap);

        foreach (var connection in _connectionViews.Where(c => _selectedNodeIds.Contains(c.Connection.SourceNodeId) || _selectedNodeIds.Contains(c.Connection.TargetNodeId)))
        {
            UpdateConnectionVisual(connection);
        }
    }

    private void OnNodeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizingNodeId.HasValue)
            return;

        if (_draggingNode is not FrameworkElement element)
            return;

        element.ReleaseMouseCapture();
        _draggingNode = null;
        _draggingNodeId = null;
        _dragNodeStartPositions.Clear();
        HideGuideLines();
        if (_didMoveNodes)
        {
            _didMoveNodes = false;
            MarkDocumentDirty();
        }
    }

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source != DesignerCanvas)
            return;

        if (_resizingNodeId.HasValue)
            return;

        _selectionAppendMode = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _canvasSelectionStart = e.GetPosition(DesignerCanvas);
        _isCanvasSelecting = true;
        _hasSelectionDragMoved = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;

        if (!_selectionAppendMode)
            _selectedNodeIds.Clear();

        _selectedConnectionId = null;
        _editingNodeId = null;
        HideGuideLines();
        HidePalettePreview();
        UpdateSelectionVisuals();
        DesignerCanvas.CaptureMouse();
        Focus();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizingNodeId.HasValue)
            return;

        if (!_isCanvasSelecting || !DesignerCanvas.IsMouseCaptured)
            return;

        var current = e.GetPosition(DesignerCanvas);
        var minX = Math.Min(_canvasSelectionStart.X, current.X);
        var minY = Math.Min(_canvasSelectionStart.Y, current.Y);
        var width = Math.Abs(current.X - _canvasSelectionStart.X);
        var height = Math.Abs(current.Y - _canvasSelectionStart.Y);

        if (!_hasSelectionDragMoved &&
            width < SystemParameters.MinimumHorizontalDragDistance &&
            height < SystemParameters.MinimumVerticalDragDistance)
            return;

        _hasSelectionDragMoved = true;
        Canvas.SetLeft(SelectionRectangle, minX);
        Canvas.SetTop(SelectionRectangle, minY);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        SelectionRectangle.Visibility = Visibility.Visible;
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizingNodeId.HasValue)
            return;

        if (!_isCanvasSelecting)
            return;

        if (DesignerCanvas.IsMouseCaptured)
            DesignerCanvas.ReleaseMouseCapture();

        if (_hasSelectionDragMoved)
        {
            var current = e.GetPosition(DesignerCanvas);
            ApplySelectionRectangle(current);
        }

        _isCanvasSelecting = false;
        _hasSelectionDragMoved = false;
        _selectionAppendMode = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnNodePreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not Guid nodeId)
            return;

        if (!_selectedNodeIds.Contains(nodeId))
            SelectNode(nodeId, false);

        e.Handled = false;
    }

    private void BeginNodeTextEdit(Guid nodeId)
    {
        _editingNodeId = nodeId;
        RenderDocument();
        SelectNode(nodeId, false);
    }

    private void CommitNodeTextEdit(Guid nodeId, string? text)
    {
        var node = _document.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
            return;

        if (!string.IsNullOrWhiteSpace(text))
            node.Text = text.Trim();

        _editingNodeId = null;
        RenderDocument();
        SelectNode(nodeId, false);
        MarkDocumentDirty();
        Focus();
    }

    private void OnInlineNodeEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not Guid nodeId)
            return;

        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            CommitNodeTextEdit(nodeId, textBox.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _editingNodeId = null;
            RenderDocument();
            SelectNode(nodeId, false);
            e.Handled = true;
        }
    }

    private void OnInlineNodeEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is Guid nodeId && _editingNodeId == nodeId)
            CommitNodeTextEdit(nodeId, textBox.Text);
    }

    private SnapResult BuildSnapResult(Guid draggingNodeId, Vector delta)
    {
        const double threshold = 10;
        var draggingNode = _document.Nodes.First(n => n.Id == draggingNodeId);
        var startPosition = _dragNodeStartPositions[draggingNodeId];
        var proposed = new Rect(startPosition.X + delta.X, startPosition.Y + delta.Y, draggingNode.Width, draggingNode.Height);

        double? bestDx = null;
        double? guideX = null;
        foreach (var other in _document.Nodes.Where(n => !_selectedNodeIds.Contains(n.Id)))
        {
            var candidates = new (double current, double target)[]
            {
                (proposed.Left, other.X),
                (proposed.Left + proposed.Width / 2, other.X + other.Width / 2),
                (proposed.Right, other.X + other.Width),
            };

            foreach (var candidate in candidates)
            {
                var diff = candidate.target - candidate.current;
                if (Math.Abs(diff) <= threshold && (bestDx == null || Math.Abs(diff) < Math.Abs(bestDx.Value)))
                {
                    bestDx = diff;
                    guideX = candidate.target;
                }
            }
        }

        double? bestDy = null;
        double? guideY = null;
        foreach (var other in _document.Nodes.Where(n => !_selectedNodeIds.Contains(n.Id)))
        {
            var candidates = new (double current, double target)[]
            {
                (proposed.Top, other.Y),
                (proposed.Top + proposed.Height / 2, other.Y + other.Height / 2),
                (proposed.Bottom, other.Y + other.Height),
            };

            foreach (var candidate in candidates)
            {
                var diff = candidate.target - candidate.current;
                if (Math.Abs(diff) <= threshold && (bestDy == null || Math.Abs(diff) < Math.Abs(bestDy.Value)))
                {
                    bestDy = diff;
                    guideY = candidate.target;
                }
            }
        }

        return new SnapResult
        {
            DeltaX = bestDx ?? 0,
            DeltaY = bestDy ?? 0,
            VerticalGuideX = guideX,
            HorizontalGuideY = guideY
        };
    }

    private void UpdateGuideLines(SnapResult snap)
    {
        if (snap.VerticalGuideX.HasValue)
        {
            VerticalGuideLine.X1 = snap.VerticalGuideX.Value;
            VerticalGuideLine.X2 = snap.VerticalGuideX.Value;
            VerticalGuideLine.Visibility = Visibility.Visible;
        }
        else
        {
            VerticalGuideLine.Visibility = Visibility.Collapsed;
        }

        if (snap.HorizontalGuideY.HasValue)
        {
            HorizontalGuideLine.Y1 = snap.HorizontalGuideY.Value;
            HorizontalGuideLine.Y2 = snap.HorizontalGuideY.Value;
            HorizontalGuideLine.Visibility = Visibility.Visible;
        }
        else
        {
            HorizontalGuideLine.Visibility = Visibility.Collapsed;
        }
    }

    private void HideGuideLines()
    {
        VerticalGuideLine.Visibility = Visibility.Collapsed;
        HorizontalGuideLine.Visibility = Visibility.Collapsed;
    }

    private void AddNode(FlowchartNodeType type, string text, double width, double height)
    {
        var index = _document.Nodes.Count;
        var node = new FlowchartNode
        {
            Type = type,
            Text = text,
            X = 120 + (index % 5) * 180,
            Y = 80 + (index / 5) * 120,
            Width = width,
            Height = height,
            ZIndex = GetNextNodeZIndex()
        };
        _document.Nodes.Add(node);
        CreateNodeVisual(node);
        SelectNode(node.Id, false);
        UpdateStatusBar();
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        ResetDocument();
    }

    private void OnOpenDocument(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "流程图文件 (*.kiteflow.json)|*.kiteflow.json|JSON 文件 (*.json)|*.json",
            Title = "打开流程图"
        };
        if (dialog.ShowDialog() != true)
            return;

        var document = _flowchartStorageService.LoadFromFile(dialog.FileName);
        if (string.IsNullOrWhiteSpace(document.Name))
            document.Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);

        _document = document;
        _currentFilePath = System.IO.Path.GetFullPath(dialog.FileName);
        _currentStorageType = FlowchartStorageType.File;
        _currentFlowchartStorageId = _flowchartStorageService.FindByFilePath(_currentFilePath)?.Id;
        _selectedNodeIds.Clear();
        _editingNodeId = null;
        _hasPendingChanges = false;
        _lastAutoSavedAt = null;
        _autoSaveTimer.Stop();
        RenderDocument();
        RefreshSavedFlowcharts();
    }

    private void OnSaveDocument(object sender, RoutedEventArgs e)
    {
        if (!SaveCurrentDocument())
            return;

        UpdateStatusBar();
    }

    private bool SaveCurrentDocument(bool forcePrompt = false, bool createNewRecord = false, bool isAutoSave = false)
    {
        if (!forcePrompt && _currentStorageType.HasValue)
        {
            try
            {
                _isAutoSaving = isAutoSave;
                var savedItem = _flowchartStorageService.Save(
                    _document,
                    string.IsNullOrWhiteSpace(_document.Name) ? "未命名流程图" : _document.Name,
                    _currentStorageType.Value,
                    _currentStorageType == FlowchartStorageType.File ? _currentFilePath : null,
                    _currentFlowchartStorageId);
                ApplySavedFlowchart(savedItem, refreshList: !isAutoSave);
                _hasPendingChanges = false;
                if (isAutoSave)
                    _lastAutoSavedAt = DateTime.Now;
                UpdateStatusBar();
                return true;
            }
            catch (Exception ex)
            {
                if (!isAutoSave)
                    MessageBox.Show($"保存流程图失败：{ex.Message}", "保存流程图", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                _isAutoSaving = false;
            }
        }

        if (isAutoSave)
            return false;

        var request = ShowSaveFlowchartDialog(
            string.IsNullOrWhiteSpace(_document.Name) ? "未命名流程图" : _document.Name,
            _currentStorageType,
            _currentFilePath);
        if (request == null)
            return false;

        var saved = _flowchartStorageService.Save(_document, request.Name, request.StorageType, request.FilePath, createNewRecord ? null : _currentFlowchartStorageId);
        ApplySavedFlowchart(saved);
        _hasPendingChanges = false;
        _lastAutoSavedAt = null;
        UpdateStatusBar();
        return true;
    }

    private void ApplySavedFlowchart(FlowchartStorageItem item, bool refreshList = true)
    {
        _currentFlowchartStorageId = item.Id;
        _currentStorageType = item.StorageType;
        _currentFilePath = item.FilePath;
        _document.Name = item.Name;

        if (refreshList)
        {
            RefreshSavedFlowcharts();
        }
        else
        {
            UpdateSavedFlowchartListItem(item);
        }

        UpdateStatusBar();
    }

    private void UpdateSavedFlowchartListItem(FlowchartStorageItem item)
    {
        var existingIndex = _savedFlowcharts
            .Select((value, index) => new { value, index })
            .FirstOrDefault(entry => entry.value.Id == item.Id)?.index;

        if (existingIndex.HasValue)
            _savedFlowcharts[existingIndex.Value] = item;
        else
            _savedFlowcharts.Insert(0, item);

        MoveSavedFlowchartsPageToItem(item.Id);
        RefreshPagedSavedFlowcharts();
    }

    private SaveFlowchartRequest? ShowSaveFlowchartDialog(string initialName, FlowchartStorageType? initialStorageType, string? initialFilePath)
    {
        var owner = Window.GetWindow(this);
        var nameTextBox = new TextBox
        {
            Text = initialName,
            Margin = new Thickness(0, 6, 0, 0),
            MinWidth = 280
        };
        var databaseRadio = new RadioButton
        {
            Content = "保存到数据库",
            Margin = new Thickness(0, 8, 0, 0),
            IsChecked = initialStorageType != FlowchartStorageType.File
        };
        var fileRadio = new RadioButton
        {
            Content = "保存到文件",
            Margin = new Thickness(0, 6, 0, 0),
            IsChecked = initialStorageType == FlowchartStorageType.File
        };
        var filePathTextBox = new TextBox
        {
            Text = initialStorageType == FlowchartStorageType.File ? initialFilePath ?? string.Empty : string.Empty,
            Margin = new Thickness(0, 6, 8, 0),
            MinWidth = 220
        };
        var browseButton = new Button
        {
            Content = "浏览...",
            MinWidth = 72,
            Margin = new Thickness(0, 6, 0, 0)
        };

        void UpdateFileControls()
        {
            var isFileMode = fileRadio.IsChecked == true;
            filePathTextBox.IsEnabled = isFileMode;
            browseButton.IsEnabled = isFileMode;
        }

        browseButton.Click += (_, _) =>
        {
            var fileDialog = new SaveFileDialog
            {
                Filter = "流程图文件 (*.kiteflow.json)|*.kiteflow.json|JSON 文件 (*.json)|*.json",
                FileName = (string.IsNullOrWhiteSpace(nameTextBox.Text) ? "未命名流程图" : nameTextBox.Text.Trim()) + ".kiteflow.json",
                Title = "选择流程图保存位置"
            };
            if (fileDialog.ShowDialog(owner) == true)
                filePathTextBox.Text = fileDialog.FileName;
        };

        databaseRadio.Checked += (_, _) => UpdateFileControls();
        fileRadio.Checked += (_, _) => UpdateFileControls();
        UpdateFileControls();

        SaveFlowchartRequest? result = null;
        var dialogWindow = new Window
        {
            Title = "保存流程图",
            Width = 460,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = owner,
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    Children =
                    {
                        new TextBlock { Text = "流程图名称", FontWeight = FontWeights.SemiBold },
                        nameTextBox,
                        new TextBlock { Text = "保存位置", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0) },
                        databaseRadio,
                        fileRadio
                    }
                }
            }
        };

        var contentGrid = (Grid)((Border)dialogWindow.Content).Child;
        Grid.SetRow(nameTextBox, 1);
        Grid.SetRow(databaseRadio, 3);
        Grid.SetRow(fileRadio, 4);

        var filePathPanel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        filePathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filePathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filePathPanel.Children.Add(filePathTextBox);
        filePathPanel.Children.Add(browseButton);
        Grid.SetColumn(browseButton, 1);
        Grid.SetRow(filePathPanel, 5);
        contentGrid.Children.Add(filePathPanel);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancelButton = new Button { Content = "取消", MinWidth = 72, Margin = new Thickness(0, 0, 8, 0) };
        var saveButton = new Button { Content = "保存", MinWidth = 72 };
        cancelButton.Click += (_, _) => dialogWindow.Close();
        saveButton.Click += (_, _) =>
        {
            var name = nameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(dialogWindow, "请先输入流程图名称。", "保存流程图", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var storageType = fileRadio.IsChecked == true ? FlowchartStorageType.File : FlowchartStorageType.Database;
            var filePath = storageType == FlowchartStorageType.File ? filePathTextBox.Text.Trim() : null;
            if (storageType == FlowchartStorageType.File && string.IsNullOrWhiteSpace(filePath))
            {
                MessageBox.Show(dialogWindow, "请选择文件保存位置。", "保存流程图", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            result = new SaveFlowchartRequest
            {
                Name = name,
                StorageType = storageType,
                FilePath = filePath
            };
            dialogWindow.DialogResult = true;
            dialogWindow.Close();
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(saveButton);
        Grid.SetRow(buttonPanel, 6);
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.Children.Add(buttonPanel);

        return dialogWindow.ShowDialog() == true ? result : null;
    }

    private void OnSavedFlowchartsListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SavedFlowchartsListBox.SelectedItem is not FlowchartStorageItem selectedItem)
            return;

        _document = _flowchartStorageService.Load(selectedItem);
        _currentFlowchartStorageId = selectedItem.Id;
        _currentStorageType = selectedItem.StorageType;
        _currentFilePath = selectedItem.FilePath;
        _selectedNodeIds.Clear();
        _editingNodeId = null;
        RenderDocument();
        RefreshSavedFlowcharts();
    }

    private void OnDesignerCanvasPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _canvasContextMenuPosition = e.GetPosition(DesignerCanvas);
        if (e.Source == DesignerCanvas)
        {
            _selectedNodeIds.Clear();
            _selectedConnectionId = null;
            _editingNodeId = null;
            UpdateSelectionVisuals();
        }
    }

    private void OnPasteAtCanvasLocation(object? sender, RoutedEventArgs e)
    {
        PasteClipboardNodes(_canvasContextMenuPosition);
    }

    private void OnConnectionMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Polyline hitArea)
            return;

        var visual = _connectionViews.FirstOrDefault(item => ReferenceEquals(item.HitArea, hitArea));
        if (visual == null)
            return;

        if (e.ClickCount >= 2)
        {
            EditConnectionLabel(visual.Connection.Id);
            e.Handled = true;
            return;
        }

        SelectConnection(visual.Connection.Id);
        e.Handled = true;
    }

    private void OnConnectionPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Polyline hitArea)
            return;

        var visual = _connectionViews.FirstOrDefault(item => ReferenceEquals(item.HitArea, hitArea));
        if (visual == null)
            return;

        SelectConnection(visual.Connection.Id);
        e.Handled = false;
    }

    private void OnDeleteConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Guid connectionId })
            return;

        _selectedConnectionId = connectionId;
        DeleteSelectedNodes();
    }

    private void OnApplyConnectionPresetLabelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ValueTuple<Guid, string> payload })
            return;

        ApplyConnectionLabel(payload.Item1, payload.Item2);
    }

    private void OnEditConnectionLabelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: Guid connectionId })
            EditConnectionLabel(connectionId);
    }

    private void OnClearConnectionLabelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Guid connectionId })
            return;

        var connection = _document.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null || string.IsNullOrWhiteSpace(connection.Label))
            return;

        ApplyConnectionLabel(connectionId, string.Empty);
    }

    private void OnToggleConnectionStyleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Guid connectionId })
            return;

        var connection = _document.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null)
            return;

        ToggleConnectionStyle(connection);
        var visual = _connectionViews.FirstOrDefault(item => item.Connection.Id == connectionId);
        if (visual != null)
            UpdateConnectionVisual(visual);

        SelectConnection(connectionId);
        MarkDocumentDirty();
    }

    private void EditConnectionLabel(Guid connectionId)
    {
        var connection = _document.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null)
            return;

        if (!TryShowConnectionLabelDialog(connection.Label, out var newLabel))
            return;

        ApplyConnectionLabel(connectionId, newLabel);
    }

    private void ApplyConnectionLabel(Guid connectionId, string? label)
    {
        var connection = _document.Connections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null)
            return;

        var normalized = label?.Trim() ?? string.Empty;
        if (string.Equals(connection.Label, normalized, StringComparison.Ordinal))
        {
            SelectConnection(connectionId);
            return;
        }

        connection.Label = normalized;
        var visual = _connectionViews.FirstOrDefault(item => item.Connection.Id == connectionId);
        if (visual != null)
            UpdateConnectionVisual(visual);

        SelectConnection(connectionId);
        MarkDocumentDirty();
    }

    private bool TryShowConnectionLabelDialog(string? initialText, out string? result)
    {
        result = null;

        var owner = Window.GetWindow(this);
        var textBox = new TextBox
        {
            Text = initialText ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 280,
            MinHeight = 92,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var okButton = new Button
        {
            Content = "确定",
            Width = 76,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "取消",
            Width = 76,
            IsCancel = true
        };
        var clearButton = new Button
        {
            Content = "清空",
            Width = 76,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttonPanel.Children.Add(clearButton);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(new TextBlock
        {
            Text = "输入显示在线段上的文字",
            Margin = new Thickness(0, 0, 0, 8),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
        });
        root.Children.Add(textBox);
        root.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = "编辑线段文字",
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 360,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false
        };

        clearButton.Click += (_, _) =>
        {
            textBox.Clear();
            textBox.Focus();
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        if (dialog.ShowDialog() != true)
            return false;

        result = textBox.Text;
        return true;
    }

    private void ToggleConnectionStyle(FlowchartConnection connection)
    {
        connection.Style = connection.Style == FlowchartConnectionStyle.Orthogonal
            ? FlowchartConnectionStyle.Straight
            : FlowchartConnectionStyle.Orthogonal;
        connection.IsHorizontalFirst = null;
        ResetConnectionRoutingState(connection);
    }

    private void OnConnectionEndpointHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not ConnectionEndpointHandleTag tag)
            return;

        var visual = _connectionViews.FirstOrDefault(item => item.Connection.Id == tag.ConnectionId);
        if (visual == null)
            return;

        _draggingConnectionId = tag.ConnectionId;
        _draggingConnectionSourceAnchor = tag.IsSource;
        _draggingConnectionHandle = ellipse;
        _didMoveConnectionAnchor = false;
        ellipse.CaptureMouse();
        SelectConnection(tag.ConnectionId);
        UpdateConnectionAnchorFromPosition(visual.Connection, tag.IsSource, e.GetPosition(DesignerCanvas));
        UpdateConnectionVisual(visual);
        e.Handled = true;
    }

    private void OnConnectionEndpointHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingConnectionId == null || _draggingConnectionHandle == null || !_draggingConnectionHandle.IsMouseCaptured)
            return;

        var visual = _connectionViews.FirstOrDefault(item => item.Connection.Id == _draggingConnectionId.Value);
        if (visual == null)
            return;

        var changed = UpdateConnectionAnchorFromPosition(visual.Connection, _draggingConnectionSourceAnchor, e.GetPosition(DesignerCanvas));
        if (!changed)
            return;

        _didMoveConnectionAnchor = true;
        UpdateConnectionVisual(visual);
        e.Handled = true;
    }

    private void OnConnectionEndpointHandleMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingConnectionHandle != null && _draggingConnectionHandle.IsMouseCaptured)
            _draggingConnectionHandle.ReleaseMouseCapture();

        if (_draggingConnectionId.HasValue)
        {
            var visual = _connectionViews.FirstOrDefault(item => item.Connection.Id == _draggingConnectionId.Value);
            if (visual != null)
            {
                var changed = UpdateConnectionAnchorFromPosition(visual.Connection, _draggingConnectionSourceAnchor, e.GetPosition(DesignerCanvas));
                _didMoveConnectionAnchor |= changed;
                UpdateConnectionVisual(visual);
            }
        }

        _draggingConnectionId = null;
        _draggingConnectionHandle = null;
        _draggingConnectionSourceAnchor = false;
        if (_didMoveConnectionAnchor)
        {
            _didMoveConnectionAnchor = false;
            MarkDocumentDirty();
        }

        e.Handled = true;
    }

    private bool UpdateConnectionAnchorFromPosition(FlowchartConnection connection, bool isSource, Point position)
    {
        var nodeId = isSource ? connection.SourceNodeId : connection.TargetNodeId;
        var node = _document.Nodes.FirstOrDefault(item => item.Id == nodeId);
        if (node == null)
            return false;

        var anchor = GetNearestAnchorPlacement(node, position);
        var currentSide = isSource ? connection.SourceAnchorSide : connection.TargetAnchorSide;
        var currentCoordinate = NormalizeAnchorCoordinate(isSource ? connection.SourceAnchorCoordinate : connection.TargetAnchorCoordinate);
        var changed = currentSide != anchor.Side || !AreClose(currentCoordinate, anchor.Coordinate);

        if (isSource)
        {
            connection.SourceAnchorSide = anchor.Side;
            connection.SourceAnchorCoordinate = anchor.Coordinate;
        }
        else
        {
            connection.TargetAnchorSide = anchor.Side;
            connection.TargetAnchorCoordinate = anchor.Coordinate;
        }

        return changed;
    }

    private void OnResizeHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border handle || handle.Tag is not Guid nodeId)
            return;

        var node = _document.Nodes.FirstOrDefault(item => item.Id == nodeId);
        if (node == null)
            return;

        SelectNode(nodeId, false);
        _resizingNodeId = nodeId;
        _resizeStartPoint = e.GetPosition(DesignerCanvas);
        _resizeStartWidth = node.Width;
        _resizeStartHeight = node.Height;
        _didResizeNode = false;
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizingNodeId == null || sender is not Border handle || !handle.IsMouseCaptured)
            return;

        var node = _document.Nodes.FirstOrDefault(item => item.Id == _resizingNodeId.Value);
        if (node == null)
            return;

        var current = e.GetPosition(DesignerCanvas);
        var newWidth = Math.Max(90, _resizeStartWidth + (current.X - _resizeStartPoint.X));
        var newHeight = Math.Max(node.Type == FlowchartNodeType.Text ? 40 : 56, _resizeStartHeight + (current.Y - _resizeStartPoint.Y));
        node.Width = newWidth;
        node.Height = newHeight;
        UpdateNodeVisualSize(node);
        _didResizeNode = true;

        foreach (var connection in _connectionViews.Where(item => item.Connection.SourceNodeId == node.Id || item.Connection.TargetNodeId == node.Id))
            UpdateConnectionVisual(connection);

        e.Handled = true;
    }

    private void OnResizeHandleMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border handle)
            return;

        if (handle.IsMouseCaptured)
            handle.ReleaseMouseCapture();

        _resizingNodeId = null;
        if (_didResizeNode)
        {
            _didResizeNode = false;
            MarkDocumentDirty();
        }
        e.Handled = true;
    }

    private void UpdateNodeVisualSize(FlowchartNode node)
    {
        if (!_nodeViews.TryGetValue(node.Id, out var element) || element is not Grid root)
            return;

        root.Width = node.Width;
        root.Height = node.Height;

        if (root.Children.Count == 0)
            return;

        switch (root.Children[0])
        {
            case Border border:
                border.Width = node.Width;
                border.Height = node.Height;
                break;
            case Grid bodyGrid:
                bodyGrid.Width = node.Width;
                bodyGrid.Height = node.Height;
                if (bodyGrid.Children.OfType<Polygon>().FirstOrDefault() is Polygon polygon)
                {
                    polygon.Points = new PointCollection
                    {
                        new Point(node.Width / 2, 0),
                        new Point(node.Width, node.Height / 2),
                        new Point(node.Width / 2, node.Height),
                        new Point(0, node.Height / 2)
                    };
                }
                break;
        }

        UpdateSelectionVisuals();
        UpdateStatusBar();
    }

    private void OnSavedFlowchartItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is FlowchartStorageItem item)
            SavedFlowchartsListBox.SelectedItem = item;
    }

    private void OnRenameSavedFlowchartClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: FlowchartStorageItem item })
            return;

        var request = ShowSaveFlowchartDialog(item.Name, item.StorageType, item.FilePath);
        if (request == null)
            return;

        var document = _flowchartStorageService.Load(item);
        var saved = _flowchartStorageService.Save(document, request.Name, request.StorageType, request.FilePath, item.Id);
        if (_currentFlowchartStorageId == item.Id)
            ApplySavedFlowchart(saved);
        else
            RefreshSavedFlowcharts();
    }

    private void OnDeleteSavedFlowchartClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: FlowchartStorageItem item })
            return;

        if (MessageBox.Show($"确定要删除流程图记录“{item.Name}”吗？如果它是文件保存，仅会删除列表记录，不会删除原文件。", "删除流程图记录", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _flowchartStorageService.Delete(item.Id);
        if (_currentFlowchartStorageId == item.Id)
        {
            _currentFlowchartStorageId = null;
            _currentStorageType = null;
            _currentFilePath = null;
            UpdateStatusBar();
        }

        RefreshSavedFlowcharts();
    }

    private void OnOpenSavedFlowchartLocationClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: FlowchartStorageItem item } || item.StorageType != FlowchartStorageType.File || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        if (!File.Exists(item.FilePath))
        {
            MessageBox.Show("文件不存在，无法打开所在位置。", "打开文件位置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FilePath}\"") { UseShellExecute = true });
    }

    private void OnSaveAsDocument(object sender, RoutedEventArgs e)
    {
        if (SaveCurrentDocument(forcePrompt: true, createNewRecord: true))
            UpdateStatusBar();
    }

    private void OnAddStartEnd(object sender, RoutedEventArgs e)
    {
        AddNode(FlowchartNodeType.StartEnd, "开始/结束", 140, 70);
    }

    private void OnAddProcess(object sender, RoutedEventArgs e)
    {
        AddNode(FlowchartNodeType.Process, "处理步骤", 150, 72);
    }

    private void OnAddDecision(object sender, RoutedEventArgs e)
    {
        AddNode(FlowchartNodeType.Decision, "条件判断", 150, 90);
    }

    private void OnAddText(object sender, RoutedEventArgs e)
    {
        AddNode(FlowchartNodeType.Text, "说明文本", 170, 60);
    }

    private void OnConnectSelected(object sender, RoutedEventArgs e)
    {
        if (_selectedNodeIds.Count != 2)
        {
            MessageBox.Show("请按住 Ctrl 选择两个节点后再连线。", "连接节点", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ids = _selectedNodeIds.ToArray();
        if (_document.Connections.Any(c => c.SourceNodeId == ids[0] && c.TargetNodeId == ids[1]))
            return;

        var connection = new FlowchartConnection
        {
            SourceNodeId = ids[0],
            TargetNodeId = ids[1],
            Style = FlowchartConnectionStyle.Orthogonal,
            IsHorizontalFirst = null
        };
        _document.Connections.Add(connection);
        CreateConnectionVisual(connection);
        SelectConnection(connection.Id);
        UpdateStatusBar();
        MarkDocumentDirty();
    }

    private void OnConnectorHandleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse ellipse || ellipse.Tag is not NodeConnectorHandleTag tag)
            return;

        _connectingSourceNodeId = tag.NodeId;
        _connectingSourceAnchorSide = tag.Side;
        _connectingSourceAnchorCoordinate = tag.Coordinate;
        _connectingHandle = ellipse;
        ellipse.CaptureMouse();

        _previewConnectionLine = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            StrokeThickness = 2,
            StrokeDashArray = [4, 4],
            IsHitTestVisible = false
        };
        _previewConnectionArrow = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_previewConnectionLine, 850);
        Panel.SetZIndex(_previewConnectionArrow, 851);
        DesignerCanvas.Children.Add(_previewConnectionLine);
        DesignerCanvas.Children.Add(_previewConnectionArrow);
        UpdatePreviewConnection(e.GetPosition(DesignerCanvas));
        e.Handled = true;
    }

    private void OnConnectorHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (_connectingSourceNodeId == null || _connectingHandle == null || !_connectingHandle.IsMouseCaptured)
            return;

        UpdatePreviewConnection(e.GetPosition(DesignerCanvas));
    }

    private void OnConnectorHandleMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_connectingHandle != null)
            _connectingHandle.ReleaseMouseCapture();

        if (_connectingSourceNodeId is Guid sourceNodeId)
        {
            var releasePosition = e.GetPosition(DesignerCanvas);
            var targetNode = FindNearestConnectableNode(sourceNodeId, releasePosition, 120);
            if (targetNode != null && !_document.Connections.Any(c => c.SourceNodeId == sourceNodeId && c.TargetNodeId == targetNode.Id))
            {
                var targetAnchor = GetNearestAnchorPlacement(targetNode, releasePosition);
                var connection = new FlowchartConnection
                {
                    SourceNodeId = sourceNodeId,
                    TargetNodeId = targetNode.Id,
                    Style = FlowchartConnectionStyle.Orthogonal,
                    IsHorizontalFirst = null,
                    SourceAnchorSide = _connectingSourceAnchorSide,
                    SourceAnchorCoordinate = _connectingSourceAnchorCoordinate,
                    TargetAnchorSide = targetAnchor.Side,
                    TargetAnchorCoordinate = targetAnchor.Coordinate
                };
                _document.Connections.Add(connection);
                CreateConnectionVisual(connection);
                SelectConnection(connection.Id);
                UpdateStatusBar();
                MarkDocumentDirty();
            }
        }

        ClearPreviewConnection();
        e.Handled = true;
    }

    private void UpdatePreviewConnection(Point currentPosition)
    {
        if (_connectingSourceNodeId is not Guid sourceNodeId || _previewConnectionLine == null || _previewConnectionArrow == null)
            return;

        var sourceNode = _document.Nodes.First(n => n.Id == sourceNodeId);
        var targetNode = FindNearestConnectableNode(sourceNodeId, currentPosition, 120);
        var sourceCenter = GetNodeCenter(sourceNode);
        var startPoint = GetConnectionAnchorPoint(sourceNode, _connectingSourceAnchorSide, _connectingSourceAnchorCoordinate, currentPosition);
        var endPoint = targetNode != null
            ? GetNearestAnchorPlacement(targetNode, currentPosition).Point
            : currentPosition;
        var points = BuildOrthogonalPath(null, startPoint, endPoint);
        _previewConnectionLine.Points = new PointCollection(points);

        var arrowBase = points.Count >= 2 ? points[^2] : startPoint;
        var angle = Math.Atan2(endPoint.Y - arrowBase.Y, endPoint.X - arrowBase.X);
        const double arrowLength = 18;
        const double arrowWidth = 8;
        var p1 = endPoint;
        var p2 = new Point(
            endPoint.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Sin(angle),
            endPoint.Y - arrowLength * Math.Sin(angle) - arrowWidth * Math.Cos(angle));
        var p3 = new Point(
            endPoint.X - arrowLength * Math.Cos(angle) - arrowWidth * Math.Sin(angle),
            endPoint.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Cos(angle));
        _previewConnectionArrow.Points = new PointCollection { p1, p2, p3 };
    }

    private FlowchartNode? FindNearestConnectableNode(Guid sourceNodeId, Point position, double maxDistance)
    {
        FlowchartNode? best = null;
        var bestDistance = maxDistance;

        foreach (var node in _document.Nodes.Where(n => n.Id != sourceNodeId))
        {
            var center = GetNodeCenter(node);
            var distance = (center - position).Length;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }

        return best;
    }

    private void ClearPreviewConnection()
    {
        if (_previewConnectionLine != null)
            DesignerCanvas.Children.Remove(_previewConnectionLine);
        if (_previewConnectionArrow != null)
            DesignerCanvas.Children.Remove(_previewConnectionArrow);

        _previewConnectionLine = null;
        _previewConnectionArrow = null;
        _connectingSourceNodeId = null;
        _connectingSourceAnchorSide = null;
        _connectingSourceAnchorCoordinate = null;
        _connectingHandle = null;
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
    {
        DeleteSelectedNodes();
    }

    private void DeleteSelectedNodes()
    {
        if (_selectedNodeIds.Count == 0 && _selectedConnectionId == null)
            return;

        if (_selectedNodeIds.Count > 0)
        {
            _document.Nodes.RemoveAll(n => _selectedNodeIds.Contains(n.Id));
            _document.Connections.RemoveAll(c => _selectedNodeIds.Contains(c.SourceNodeId) || _selectedNodeIds.Contains(c.TargetNodeId));
        }

        if (_selectedConnectionId.HasValue)
            _document.Connections.RemoveAll(c => c.Id == _selectedConnectionId.Value);

        _selectedNodeIds.Clear();
        _selectedConnectionId = null;
        _editingNodeId = null;
        RenderDocument();
        MarkDocumentDirty();
    }

    private void CopySelectedNodesToClipboard()
    {
        if (_selectedNodeIds.Count == 0)
            return;

        var selectedNodes = _document.Nodes
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .OrderBy(node => node.Y)
            .ThenBy(node => node.X)
            .ToList();

        _clipboardNodes = selectedNodes
            .Select(node => new ClipboardNodeSnapshot
            {
                Node = new FlowchartNode
                {
                    Id = node.Id,
                    Type = node.Type,
                    Text = node.Text,
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height,
                    ZIndex = node.ZIndex
                }
            })
            .ToList();

        var selectedNodeIds = selectedNodes.Select(node => node.Id).ToHashSet();
        _clipboardConnections = _document.Connections
            .Where(connection => selectedNodeIds.Contains(connection.SourceNodeId) && selectedNodeIds.Contains(connection.TargetNodeId))
            .Select(connection => new ClipboardConnectionSnapshot
            {
                SourceNodeId = connection.SourceNodeId,
                TargetNodeId = connection.TargetNodeId,
                Label = connection.Label,
                Style = connection.Style,
                IsHorizontalFirst = connection.IsHorizontalFirst,
                SourceAnchorSide = connection.SourceAnchorSide,
                TargetAnchorSide = connection.TargetAnchorSide,
                SourceAnchorCoordinate = connection.SourceAnchorCoordinate,
                TargetAnchorCoordinate = connection.TargetAnchorCoordinate,
                ControlPoints = connection.ControlPoints
                    .Select(point => new FlowchartConnectionControlPoint
                    {
                        Coordinate = point.Coordinate,
                        DisplayCoordinate = point.DisplayCoordinate
                    })
                    .ToList()
            })
            .ToList();

        _pasteSequence = 0;
    }

    private void ApplySelectionRectangle(Point endPoint)
    {
        var left = Math.Min(_canvasSelectionStart.X, endPoint.X);
        var top = Math.Min(_canvasSelectionStart.Y, endPoint.Y);
        var width = Math.Abs(endPoint.X - _canvasSelectionStart.X);
        var height = Math.Abs(endPoint.Y - _canvasSelectionStart.Y);
        var selectionRect = new Rect(left, top, width, height);

        var intersectingNodeIds = _document.Nodes
            .Where(node => selectionRect.IntersectsWith(new Rect(node.X, node.Y, node.Width, node.Height)))
            .Select(node => node.Id)
            .ToList();

        if (!_selectionAppendMode)
            _selectedNodeIds.Clear();

        foreach (var nodeId in intersectingNodeIds)
            _selectedNodeIds.Add(nodeId);

        UpdateSelectionVisuals();
    }

    private void PasteClipboardNodes(Point? targetPosition = null)
    {
        if (_clipboardNodes.Count == 0)
            return;

        _pasteSequence++;
        Vector offset;
        if (targetPosition.HasValue)
        {
            var minX = _clipboardNodes.Min(item => item.Node.X);
            var minY = _clipboardNodes.Min(item => item.Node.Y);
            offset = new Vector(targetPosition.Value.X - minX, targetPosition.Value.Y - minY);
        }
        else
        {
            offset = new Vector(PasteOffsetStep * _pasteSequence, PasteOffsetStep * _pasteSequence);
        }
        var idMap = new Dictionary<Guid, Guid>();
        var nextZIndex = GetNextNodeZIndex();

        _selectedNodeIds.Clear();
        _selectedConnectionId = null;

        foreach (var snapshot in _clipboardNodes)
        {
            var source = snapshot.Node;
            var clone = new FlowchartNode
            {
                Id = Guid.NewGuid(),
                Type = source.Type,
                Text = source.Text,
                X = Math.Max(0, source.X + offset.X),
                Y = Math.Max(0, source.Y + offset.Y),
                Width = source.Width,
                Height = source.Height,
                ZIndex = nextZIndex++
            };

            idMap[source.Id] = clone.Id;
            _document.Nodes.Add(clone);
            _selectedNodeIds.Add(clone.Id);
        }

        var pastedConnections = _clipboardConnections
            .Where(connection => idMap.ContainsKey(connection.SourceNodeId) && idMap.ContainsKey(connection.TargetNodeId))
            .Select(connection => new FlowchartConnection
            {
                SourceNodeId = idMap[connection.SourceNodeId],
                TargetNodeId = idMap[connection.TargetNodeId],
                Label = connection.Label,
                Style = connection.Style,
                IsHorizontalFirst = connection.IsHorizontalFirst,
                SourceAnchorSide = connection.SourceAnchorSide,
                TargetAnchorSide = connection.TargetAnchorSide,
                SourceAnchorCoordinate = connection.SourceAnchorCoordinate,
                TargetAnchorCoordinate = connection.TargetAnchorCoordinate,
                ControlPoints = connection.ControlPoints
                    .Select(point => new FlowchartConnectionControlPoint
                    {
                        Coordinate = point.Coordinate,
                        DisplayCoordinate = point.DisplayCoordinate
                    })
                    .ToList()
            })
            .ToList();
        _document.Connections.AddRange(pastedConnections);

        _editingNodeId = null;
        RenderDocument();
        MarkDocumentDirty();
    }

    private void OnApplyNodeText(object sender, RoutedEventArgs e)
    {
        if (_editingNodeId is not Guid nodeId)
            return;

        CommitNodeTextEdit(nodeId, null);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        SelectAllNodes();
    }

    private void OnZoomSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ApplyZoom(e.NewValue);
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Min(2.0, ZoomSlider.Value + 0.1);
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = Math.Max(0.5, ZoomSlider.Value - 0.1);
    }

    private void OnZoomReset(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = 1.0;
    }

    private void ApplyZoom(double zoom)
    {
        _zoomLevel = zoom;
        CanvasScaleTransform.ScaleX = zoom;
        CanvasScaleTransform.ScaleY = zoom;
        UpdateStatusBar();
    }

    private void OnDesignerMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        var delta = e.Delta > 0 ? 0.1 : -0.1;
        ZoomSlider.Value = Math.Max(0.5, Math.Min(2.0, ZoomSlider.Value + delta));
        e.Handled = true;
    }

    private void OnPagePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_editingNodeId.HasValue)
            return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.A)
        {
            SelectAllNodes();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.C)
        {
            CopySelectedNodesToClipboard();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.D)
        {
            DuplicateSelection();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.X)
        {
            CutSelectedNodes();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.V)
        {
            PasteClipboardNodes();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            DeleteSelectedNodes();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && _selectedConnectionId.HasValue)
        {
            EditConnectionLabel(_selectedConnectionId.Value);
            e.Handled = true;
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        Vector? delta = e.Key switch
        {
            Key.Left => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up => new Vector(0, -step),
            Key.Down => new Vector(0, step),
            _ => null
        };

        if (delta.HasValue && _selectedNodeIds.Count > 0)
        {
            MoveSelectedNodes(delta.Value);
            HideGuideLines();
            e.Handled = true;
        }
    }

    private void MoveSelectedNodes(Vector delta)
    {
        foreach (var selectedNodeId in _selectedNodeIds)
        {
            var node = _document.Nodes.First(n => n.Id == selectedNodeId);
            node.X = Math.Max(0, node.X + delta.X);
            node.Y = Math.Max(0, node.Y + delta.Y);

            if (_nodeViews.TryGetValue(selectedNodeId, out var nodeView))
            {
                Canvas.SetLeft(nodeView, node.X);
                Canvas.SetTop(nodeView, node.Y);
            }
        }

        foreach (var connection in _connectionViews.Where(c => _selectedNodeIds.Contains(c.Connection.SourceNodeId) || _selectedNodeIds.Contains(c.Connection.TargetNodeId)))
            UpdateConnectionVisual(connection);

        UpdateStatusBar();
        MarkDocumentDirty();
    }

    private void OnPaletteItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is string typeName)
        {
            _palettePressedButton = button;
            _paletteDragStart = e.GetPosition(this);
            _paletteDragType = typeName;
            _paletteSuppressMouseUpAdd = false;
            _paletteSuppressClickAdd = false;
        }
    }

    private void OnPaletteItemMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Button button || !ReferenceEquals(_palettePressedButton, button) || _paletteDragType == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _paletteDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _paletteDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _paletteSuppressMouseUpAdd = true;
        _paletteSuppressClickAdd = true;
        var data = new DataObject(typeof(string), _paletteDragType);
        try
        {
            DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
        }
        finally
        {
            _paletteSuppressMouseUpAdd = true;
            _paletteDragType = null;
            HidePalettePreview();
        }
    }

    private void OnPaletteItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string typeName || !Enum.TryParse<FlowchartNodeType>(typeName, out var nodeType))
        {
            _palettePressedButton = null;
            _paletteDragType = null;
            _paletteSuppressMouseUpAdd = false;
            _paletteSuppressClickAdd = false;
            return;
        }

        if (_paletteSuppressClickAdd || _paletteSuppressMouseUpAdd || !ReferenceEquals(_palettePressedButton, button))
        {
            _palettePressedButton = null;
            _paletteSuppressMouseUpAdd = false;
            _paletteSuppressClickAdd = false;
            _paletteDragType = null;
            return;
        }

        if (ShouldSuppressPaletteDuplicate($"click:{typeName}"))
        {
            _palettePressedButton = null;
            _paletteDragType = null;
            return;
        }

        AddNodeFromPalette(nodeType);
        _palettePressedButton = null;
        _paletteDragType = null;
        _paletteSuppressMouseUpAdd = false;
        _paletteSuppressClickAdd = false;
    }

    private void OnDesignerCanvasDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(string)) &&
            e.Data.GetData(typeof(string)) is string typeName &&
            Enum.TryParse<FlowchartNodeType>(typeName, out var nodeType))
        {
            e.Effects = DragDropEffects.Copy;
            ShowPalettePreview(nodeType, e.GetPosition(DesignerCanvas));
            e.Handled = true;
            return;
        }

        HidePalettePreview();
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDesignerCanvasDragLeave(object sender, DragEventArgs e)
    {
        HidePalettePreview();
    }

    private void OnDesignerCanvasDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(string)))
            return;

        if (e.Data.GetData(typeof(string)) is not string typeName || !Enum.TryParse<FlowchartNodeType>(typeName, out var nodeType))
            return;

        var dropPoint = e.GetPosition(DesignerCanvas);
        HidePalettePreview();
        if (ShouldSuppressPaletteDuplicate($"drop:{typeName}:{Math.Round(dropPoint.X / 8)}:{Math.Round(dropPoint.Y / 8)}"))
        {
            _palettePressedButton = null;
            _paletteDragType = null;
            _paletteSuppressMouseUpAdd = true;
            _paletteSuppressClickAdd = true;
            e.Handled = true;
            return;
        }

        AddNodeAt(nodeType, dropPoint.X, dropPoint.Y);
        _palettePressedButton = null;
        _paletteDragType = null;
        _paletteSuppressMouseUpAdd = true;
        _paletteSuppressClickAdd = true;
        e.Handled = true;
    }

    private bool ShouldSuppressPaletteDuplicate(string key)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(_lastPaletteAddKey, key, StringComparison.Ordinal) && now - _lastPaletteAddAt <= PaletteDuplicateSuppressWindow)
            return true;

        _lastPaletteAddKey = key;
        _lastPaletteAddAt = now;
        return false;
    }

    private void AddNodeAt(FlowchartNodeType type, double x, double y)
    {
        var (text, width, height) = GetNodeTemplate(type);
        var node = new FlowchartNode
        {
            Type = type,
            Text = text,
            X = Math.Max(0, x - width / 2),
            Y = Math.Max(0, y - height / 2),
            Width = width,
            Height = height,
            ZIndex = GetNextNodeZIndex()
        };
        _document.Nodes.Add(node);
        CreateNodeVisual(node);
        SelectNode(node.Id, false);
        UpdateStatusBar();
        MarkDocumentDirty();
    }

    private void AddNodeFromPalette(FlowchartNodeType type)
    {
        var (text, width, height) = GetNodeTemplate(type);
        AddNode(type, text, width, height);
    }

    private int GetNextNodeZIndex()
    {
        return _document.Nodes.Count == 0
            ? DefaultNodeZIndex
            : _document.Nodes.Max(node => node.ZIndex) + 1;
    }

    private void ShowPalettePreview(FlowchartNodeType type, Point position)
    {
        var (text, width, height) = GetNodeTemplate(type);
        PalettePreviewText.Text = text;
        PalettePreview.Width = width;
        PalettePreview.Height = height;
        PalettePreview.CornerRadius = type == FlowchartNodeType.StartEnd ? new CornerRadius(height / 2) : new CornerRadius(10);

        Canvas.SetLeft(PalettePreview, Math.Max(0, position.X - width / 2));
        Canvas.SetTop(PalettePreview, Math.Max(0, position.Y - height / 2));
        PalettePreview.Visibility = Visibility.Visible;
    }

    private void HidePalettePreview()
    {
        PalettePreview.Visibility = Visibility.Collapsed;
    }

    private void OnCopySelected(object? sender, RoutedEventArgs e)
    {
        DuplicateSelection();
    }

    private void OnCutSelected(object? sender, RoutedEventArgs e)
    {
        CutSelectedNodes();
    }

    private void OnPasteClipboard(object? sender, RoutedEventArgs e)
    {
        PasteClipboardNodes();
    }

    private void DuplicateSelection()
    {
        CopySelectedNodesToClipboard();
        PasteClipboardNodes();
    }

    private void CutSelectedNodes()
    {
        if (_selectedNodeIds.Count == 0)
            return;

        CopySelectedNodesToClipboard();
        DeleteSelectedNodes();
    }

    private void DuplicateSelectedNodes()
    {
        if (_selectedNodeIds.Count == 0)
            return;

        var selectedNodes = _document.Nodes
            .Where(node => _selectedNodeIds.Contains(node.Id))
            .OrderBy(node => node.ZIndex)
            .ToList();

        if (selectedNodes.Count == 0)
            return;

        var idMap = new Dictionary<Guid, Guid>();
        var nextZIndex = GetNextNodeZIndex();
        var newNodeIds = new HashSet<Guid>();

        foreach (var node in selectedNodes)
        {
            var copy = new FlowchartNode
            {
                Id = Guid.NewGuid(),
                Type = node.Type,
                Text = node.Text,
                X = Math.Max(0, node.X + 24),
                Y = Math.Max(0, node.Y + 24),
                Width = node.Width,
                Height = node.Height,
                ZIndex = nextZIndex++
            };
            idMap[node.Id] = copy.Id;
            newNodeIds.Add(copy.Id);
            _document.Nodes.Add(copy);
        }

        var copiedConnections = _document.Connections
            .Where(connection => idMap.ContainsKey(connection.SourceNodeId) && idMap.ContainsKey(connection.TargetNodeId))
            .Select(connection => new FlowchartConnection
            {
                SourceNodeId = idMap[connection.SourceNodeId],
                TargetNodeId = idMap[connection.TargetNodeId],
                Label = connection.Label,
                Style = connection.Style,
                IsHorizontalFirst = connection.IsHorizontalFirst,
                SourceAnchorSide = connection.SourceAnchorSide,
                TargetAnchorSide = connection.TargetAnchorSide,
                SourceAnchorCoordinate = connection.SourceAnchorCoordinate,
                TargetAnchorCoordinate = connection.TargetAnchorCoordinate,
                ControlPoints = connection.ControlPoints
                    .Select(point => new FlowchartConnectionControlPoint
                    {
                        Coordinate = point.Coordinate,
                        DisplayCoordinate = point.DisplayCoordinate
                    })
                    .ToList()
            })
            .ToList();
        _document.Connections.AddRange(copiedConnections);

        _selectedNodeIds.Clear();
        foreach (var nodeId in newNodeIds)
            _selectedNodeIds.Add(nodeId);

        _editingNodeId = null;
        RenderDocument();
        MarkDocumentDirty();
    }

    private void OnBringToFrontSelected(object? sender, RoutedEventArgs e)
    {
        if (_selectedNodeIds.Count == 0)
            return;

        var nextZIndex = _document.Nodes.Max(node => node.ZIndex) + 1;
        foreach (var node in _document.Nodes.Where(node => _selectedNodeIds.Contains(node.Id)).OrderBy(node => node.ZIndex))
            node.ZIndex = nextZIndex++;

        NormalizeNodeZIndexes();
        RefreshNodeZIndexes();
        MarkDocumentDirty();
    }

    private void OnSendToBackSelected(object? sender, RoutedEventArgs e)
    {
        if (_selectedNodeIds.Count == 0)
            return;

        var nextZIndex = _document.Nodes.Min(node => node.ZIndex) - _selectedNodeIds.Count;
        foreach (var node in _document.Nodes.Where(node => _selectedNodeIds.Contains(node.Id)).OrderBy(node => node.ZIndex))
            node.ZIndex = nextZIndex++;

        NormalizeNodeZIndexes();
        RefreshNodeZIndexes();
        MarkDocumentDirty();
    }

    private Rect GetDocumentBounds()
    {
        if (_document.Nodes.Count == 0)
            return new Rect(0, 0, DesignerCanvas.Width, DesignerCanvas.Height);

        var minX = _document.Nodes.Min(node => node.X);
        var minY = _document.Nodes.Min(node => node.Y);
        var maxX = _document.Nodes.Max(node => node.X + node.Width);
        var maxY = _document.Nodes.Max(node => node.Y + node.Height);
        const double padding = 40;
        return new Rect(
            Math.Max(0, minX - padding),
            Math.Max(0, minY - padding),
            Math.Min(DesignerCanvas.Width, maxX - minX + padding * 2),
            Math.Min(DesignerCanvas.Height, maxY - minY + padding * 2));
    }

    private BitmapSource RenderDocumentBitmap(ExportImageOptions options)
    {
        var bounds = GetExportBounds(options.Scope);
        var scale = Math.Max(1, options.Scale);
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
            context.PushClip(new RectangleGeometry(new Rect(0, 0, bounds.Width, bounds.Height)));
            context.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));

            DrawExportGrid(context, bounds);
            DrawExportConnections(context);
            DrawExportNodes(context);

            context.Pop();
            context.Pop();
        }

        var dpi = 96 * scale;
        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private Rect GetExportBounds(ExportImageScope scope)
    {
        return scope switch
        {
            ExportImageScope.VisibleArea => GetVisibleCanvasBounds(),
            ExportImageScope.EntireCanvas => new Rect(0, 0, DesignerCanvas.Width, DesignerCanvas.Height),
            _ => GetDocumentBounds()
        };
    }

    private Rect GetVisibleCanvasBounds()
    {
        var zoom = Math.Max(0.01, _zoomLevel);
        var left = DesignerScrollViewer.HorizontalOffset / zoom;
        var top = DesignerScrollViewer.VerticalOffset / zoom;
        var viewportWidth = (DesignerScrollViewer.ViewportWidth > 0 ? DesignerScrollViewer.ViewportWidth : DesignerScrollViewer.ActualWidth) / zoom;
        var viewportHeight = (DesignerScrollViewer.ViewportHeight > 0 ? DesignerScrollViewer.ViewportHeight : DesignerScrollViewer.ActualHeight) / zoom;
        var width = Math.Max(1, Math.Min(DesignerCanvas.Width - left, viewportWidth));
        var height = Math.Max(1, Math.Min(DesignerCanvas.Height - top, viewportHeight));
        return new Rect(Math.Max(0, left), Math.Max(0, top), width, height);
    }

    private static void DrawExportGrid(DrawingContext context, Rect bounds)
    {
        const double gridSize = 32;
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(216, 221, 230)), 1);
        pen.Freeze();

        var startX = Math.Floor(bounds.X / gridSize) * gridSize;
        var startY = Math.Floor(bounds.Y / gridSize) * gridSize;

        for (var x = startX; x <= bounds.Right; x += gridSize)
            context.DrawLine(pen, new Point(x, bounds.Y), new Point(x, bounds.Bottom));

        for (var y = startY; y <= bounds.Bottom; y += gridSize)
            context.DrawLine(pen, new Point(bounds.X, y), new Point(bounds.Right, y));
    }

    private void DrawExportConnections(DrawingContext context)
    {
        var strokeBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
        strokeBrush.Freeze();
        var pen = new Pen(strokeBrush, 2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();

        foreach (var connection in _document.Connections)
        {
            var source = _document.Nodes.FirstOrDefault(node => node.Id == connection.SourceNodeId);
            var target = _document.Nodes.FirstOrDefault(node => node.Id == connection.TargetNodeId);
            if (source == null || target == null)
                continue;

            var sourceCenter = GetNodeCenter(source);
            var targetCenter = GetNodeCenter(target);
            var start = GetConnectionAnchorPoint(source, connection.SourceAnchorSide, connection.SourceAnchorCoordinate, targetCenter);
            var end = GetConnectionAnchorPoint(target, connection.TargetAnchorSide, connection.TargetAnchorCoordinate, sourceCenter);
            var points = BuildConnectionPath(connection, start, end);
            if (points.Count < 2)
                continue;

            var geometry = new StreamGeometry();
            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(points[0], false, false);
                geometryContext.PolyLineTo(points.Skip(1).ToArray(), true, true);
            }
            geometry.Freeze();
            context.DrawGeometry(null, pen, geometry);

            var arrowBase = points.Count >= 2 ? points[^2] : start;
            var angle = Math.Atan2(end.Y - arrowBase.Y, end.X - arrowBase.X);
            const double arrowLength = 18;
            const double arrowWidth = 8;
            var p1 = end;
            var p2 = new Point(
                end.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Sin(angle),
                end.Y - arrowLength * Math.Sin(angle) - arrowWidth * Math.Cos(angle));
            var p3 = new Point(
                end.X - arrowLength * Math.Cos(angle) - arrowWidth * Math.Sin(angle),
                end.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Cos(angle));
            var arrowGeometry = new StreamGeometry();
            using (var arrowContext = arrowGeometry.Open())
            {
                arrowContext.BeginFigure(p1, true, true);
                arrowContext.LineTo(p2, true, true);
                arrowContext.LineTo(p3, true, true);
            }
            arrowGeometry.Freeze();
            context.DrawGeometry(strokeBrush, null, arrowGeometry);
        }
    }

    private void DrawExportNodes(DrawingContext context)
    {
        foreach (var node in _document.Nodes.OrderBy(node => node.ZIndex))
        {
            switch (node.Type)
            {
                case FlowchartNodeType.StartEnd:
                    DrawEllipseNode(context, node);
                    break;
                case FlowchartNodeType.Decision:
                    DrawDecisionNode(context, node);
                    break;
                case FlowchartNodeType.Text:
                    DrawTextShapeNode(context, node);
                    break;
                default:
                    DrawProcessNode(context, node);
                    break;
            }

            DrawNodeLabel(context, node);
        }
    }

    private static void DrawProcessNode(DrawingContext context, FlowchartNode node)
    {
        var fill = new SolidColorBrush(Color.FromRgb(250, 250, 250));
        var stroke = new SolidColorBrush(Color.FromRgb(107, 114, 128));
        fill.Freeze();
        stroke.Freeze();
        var pen = new Pen(stroke, 2);
        pen.Freeze();
        context.DrawRoundedRectangle(fill, pen, new Rect(node.X, node.Y, node.Width, node.Height), 8, 8);
    }

    private static void DrawEllipseNode(DrawingContext context, FlowchartNode node)
    {
        var fill = new SolidColorBrush(Color.FromRgb(240, 249, 255));
        var stroke = new SolidColorBrush(Color.FromRgb(14, 116, 144));
        fill.Freeze();
        stroke.Freeze();
        var pen = new Pen(stroke, 2);
        pen.Freeze();
        context.DrawEllipse(fill, pen, new Point(node.X + node.Width / 2, node.Y + node.Height / 2), node.Width / 2, node.Height / 2);
    }

    private static void DrawDecisionNode(DrawingContext context, FlowchartNode node)
    {
        var fill = new SolidColorBrush(Color.FromRgb(255, 251, 235));
        var stroke = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        fill.Freeze();
        stroke.Freeze();
        var pen = new Pen(stroke, 2);
        pen.Freeze();
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(new Point(node.X + node.Width / 2, node.Y), true, true);
            geometryContext.LineTo(new Point(node.X + node.Width, node.Y + node.Height / 2), true, true);
            geometryContext.LineTo(new Point(node.X + node.Width / 2, node.Y + node.Height), true, true);
            geometryContext.LineTo(new Point(node.X, node.Y + node.Height / 2), true, true);
        }
        geometry.Freeze();
        context.DrawGeometry(fill, pen, geometry);
    }

    private static void DrawTextShapeNode(DrawingContext context, FlowchartNode node)
    {
        var stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        stroke.Freeze();
        var pen = new Pen(stroke, 1.5)
        {
            DashStyle = new DashStyle([4, 2], 0)
        };
        pen.Freeze();
        context.DrawRoundedRectangle(Brushes.White, pen, new Rect(node.X, node.Y, node.Width, node.Height), 4, 4);
    }

    private void DrawNodeLabel(DrawingContext context, FlowchartNode node)
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formattedText = new FormattedText(
            node.Text ?? string.Empty,
            CultureInfo.GetCultureInfo("zh-CN"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            14,
            new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(10, node.Width - 24),
            MaxTextHeight = Math.Max(10, node.Height - 16),
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.None
        };

        var x = node.X + (node.Width - formattedText.WidthIncludingTrailingWhitespace) / 2;
        var y = node.Y + (node.Height - formattedText.Height) / 2;
        context.DrawText(formattedText, new Point(x, y));
    }

    private ExportImageOptions? ShowExportImageOptionsDialog()
    {
        var owner = Window.GetWindow(this);
        ExportImageOptions? result = new ExportImageOptions
        {
            Scope = ExportImageScope.ContentBounds,
            Scale = 3
        };

        var visibleRadio = new RadioButton
        {
            Content = "导出当前可见区域",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var contentBoundsRadio = new RadioButton
        {
            Content = "导出所有组件最小外包矩形",
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = true
        };
        var allCanvasRadio = new RadioButton
        {
            Content = "导出整个画布内容"
        };

        var scaleLabel = new TextBlock
        {
            Text = "导出清晰度",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 8)
        };
        var scaleComboBox = new ComboBox
        {
            SelectedIndex = 2,
            MinWidth = 120
        };
        scaleComboBox.Items.Add(new ComboBoxItem { Content = "标准 1x", Tag = 1.0 });
        scaleComboBox.Items.Add(new ComboBoxItem { Content = "高清 2x", Tag = 2.0 });
        scaleComboBox.Items.Add(new ComboBoxItem { Content = "超清 3x", Tag = 3.0 });

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "选择导出范围",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(visibleRadio);
        panel.Children.Add(contentBoundsRadio);
        panel.Children.Add(allCanvasRadio);
        panel.Children.Add(scaleLabel);
        panel.Children.Add(scaleComboBox);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button { Content = "取消", MinWidth = 72, Margin = new Thickness(0, 0, 8, 0) };
        var okButton = new Button { Content = "确定", MinWidth = 72 };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = "导出图片",
            Width = 360,
            Height = 300,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            Content = panel
        };

        cancelButton.Click += (_, _) =>
        {
            result = null;
            window.DialogResult = false;
            window.Close();
        };
        okButton.Click += (_, _) =>
        {
            var scope = visibleRadio.IsChecked == true
                ? ExportImageScope.VisibleArea
                : allCanvasRadio.IsChecked == true
                    ? ExportImageScope.EntireCanvas
                    : ExportImageScope.ContentBounds;
            var scale = scaleComboBox.SelectedItem is ComboBoxItem { Tag: double selectedScale } ? selectedScale : 3.0;
            result = new ExportImageOptions
            {
                Scope = scope,
                Scale = scale
            };
            window.DialogResult = true;
            window.Close();
        };

        return window.ShowDialog() == true ? result : null;
    }

    private void OnCopyCanvasAsImage(object? sender, RoutedEventArgs e)
    {
        var options = ShowExportImageOptionsDialog();
        if (options == null)
            return;

        Clipboard.SetImage(RenderDocumentBitmap(options));
    }

    private void OnExportCanvasAsPng(object? sender, RoutedEventArgs e)
    {
        var options = ShowExportImageOptionsDialog();
        if (options == null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG 图片 (*.png)|*.png",
            FileName = $"{(string.IsNullOrWhiteSpace(_document.Name) ? "流程图" : _document.Name)}.png",
            Title = "导出流程图 PNG"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderDocumentBitmap(options)));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
    }

    private void NormalizeNodeZIndexes()
    {
        var orderedNodes = _document.Nodes.OrderBy(node => node.ZIndex).ToList();
        for (var index = 0; index < orderedNodes.Count; index++)
            orderedNodes[index].ZIndex = DefaultNodeZIndex + index;
    }

    private void RefreshNodeZIndexes()
    {
        foreach (var node in _document.Nodes)
        {
            if (_nodeViews.TryGetValue(node.Id, out var nodeView))
                Panel.SetZIndex(nodeView, node.ZIndex);
        }
    }

    private static (string Text, double Width, double Height) GetNodeTemplate(FlowchartNodeType type)
    {
        return type switch
        {
            FlowchartNodeType.StartEnd => ("开始/结束", 140, 70),
            FlowchartNodeType.Process => ("处理步骤", 150, 72),
            FlowchartNodeType.Decision => ("条件判断", 150, 90),
            FlowchartNodeType.Text => ("说明文本", 170, 60),
            _ => ("节点", 150, 72)
        };
    }
}
