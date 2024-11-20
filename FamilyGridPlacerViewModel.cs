using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Collections.Generic;

namespace RevitFamilyGridPlacer
{
    public class FamilyGridPlacerViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private FamilySymbol _selectedFamilyType;
        private Room _selectedRoom;
        private double _uDistance = 2.0;
        private double _vDistance = 2.0;
        private double _wallOffset = -0.5;
        private bool _useUpperSurface = true;
        private double _defaultHeight = 1.5;
        #endregion

        #region Properties
        public ObservableCollection<FamilySymbol> FamilyTypes { get; private set; }
        public ObservableCollection<Room> Rooms { get; private set; }

        public FamilySymbol SelectedFamilyType
        {
            get => _selectedFamilyType;
            set
            {
                _selectedFamilyType = value;
                OnPropertyChanged(nameof(SelectedFamilyType));
            }
        }

        public Room SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged(nameof(SelectedRoom));
            }
        }

        public double UDistance
        {
            get => _uDistance;
            set
            {
                _uDistance = value;
                OnPropertyChanged(nameof(UDistance));
            }
        }

        public double VDistance
        {
            get => _vDistance;
            set
            {
                _vDistance = value;
                OnPropertyChanged(nameof(VDistance));
            }
        }

        public double WallOffset
        {
            get => _wallOffset;
            set
            {
                _wallOffset = value;
                OnPropertyChanged(nameof(WallOffset));
            }
        }

        public bool UseUpperSurface
        {
            get => _useUpperSurface;
            set
            {
                _useUpperSurface = value;
                OnPropertyChanged(nameof(UseUpperSurface));
            }
        }

        public double DefaultHeight
        {
            get => _defaultHeight;
            set
            {
                _defaultHeight = value;
                OnPropertyChanged(nameof(DefaultHeight));
            }
        }

        public ICommand PlaceFamiliesCommand { get; private set; }
        public ICommand SelectRoomCommand { get; private set; }
        #endregion

        #region Constructor
        public FamilyGridPlacerViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;

            // Initialize commands
            PlaceFamiliesCommand = new RelayCommand(PlaceFamilies, CanPlaceFamilies);
            SelectRoomCommand = new RelayCommand(SelectRoom);

            // Load data
            LoadFamilyTypes();
            LoadRooms();
        }
        #endregion

        #region Private Methods
        private void LoadFamilyTypes()
        {
            FamilyTypes = new ObservableCollection<FamilySymbol>();
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType();

            foreach (FamilySymbol symbol in collector)
            {
                if (symbol != null && symbol.Family != null && CanHostByFace(symbol))
                {
                    FamilyTypes.Add(symbol);
                }
            }
        }
        public void Dispose()
        {
            // Clean up resources if needed
        }
        private bool CanHostByFace(FamilySymbol symbol)
        {
            if (symbol.Family == null) return false;
            return true; // You might want to add more specific checks here
        }

        private void LoadRooms()
        {
            Rooms = new ObservableCollection<Room>();
            var collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();

            foreach (Room room in collector)
            {
                if (room != null && room.Area > 0)
                {
                    Rooms.Add(room);
                }
            }
        }

        private bool CanPlaceFamilies()
        {
            return SelectedFamilyType != null && SelectedRoom != null;
        }

        private void SelectRoom()
        {
            try
            {
                // Hide the window temporarily
                if (System.Windows.Application.Current.MainWindow != null)
                    System.Windows.Application.Current.MainWindow.Hide();

                // Let user select a room
                Reference reference = _uidoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new RoomSelectionFilter(),
                    "Select a room");

                if (reference != null)
                {
                    Room room = _doc.GetElement(reference.ElementId) as Room;
                    if (room != null)
                    {
                        SelectedRoom = room;
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled selection
            }
            finally
            {
                // Show the window again
                if (System.Windows.Application.Current.MainWindow != null)
                    System.Windows.Application.Current.MainWindow.Show();
            }
        }

        private void PlaceFamilies()
        {
            if (!ValidateInputs()) return;

            try
            {
                using (Transaction trans = new Transaction(_doc, "Place Families in Grid"))
                {
                    trans.Start();

                    // Get room boundaries
                    var options = new SpatialElementBoundaryOptions();
                    IList<IList<BoundarySegment>> boundaries = SelectedRoom.GetBoundarySegments(options);

                    if (boundaries == null || !boundaries.Any())
                    {
                        ShowError("Selected room has no valid boundaries.");
                        return;
                    }

                    // Get room geometry
                    BoundingBoxXYZ roomBBox = SelectedRoom.get_BoundingBox(null);
                    double minX = roomBBox.Min.X + WallOffset;
                    double minY = roomBBox.Min.Y + WallOffset;
                    double maxX = roomBBox.Max.X - WallOffset;
                    double maxY = roomBBox.Max.Y - WallOffset;
                    double roomZ = roomBBox.Min.Z;

                    List<XYZ> placementPoints = new List<XYZ>();

                    // Create grid of points
                    for (double x = minX; x <= maxX; x += UDistance)
                    {
                        for (double y = minY; y <= maxY; y += VDistance)
                        {
                            XYZ point = new XYZ(x, y, roomZ);

                            // Check if point is inside room
                            if (IsPointInRoom(point, SelectedRoom))
                            {
                                double height = UseUpperSurface ?
                                    SelectedRoom.get_Parameter(BuiltInParameter.ROOM_HEIGHT).AsDouble() :
                                    DefaultHeight;

                                XYZ finalPoint = new XYZ(point.X, point.Y, roomZ + height);
                                placementPoints.Add(finalPoint);
                            }
                        }
                    }

                    // Place family instances
                    int placedCount = 0;
                    foreach (XYZ point in placementPoints)
                    {
                        try
                        {
                            if (!SelectedFamilyType.IsActive)
                                SelectedFamilyType.Activate();

                            _doc.Create.NewFamilyInstance(
                                point,
                                SelectedFamilyType,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            placedCount++;
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Error placing instance at point {point}: {ex.Message}");
                        }
                    }

                    trans.Commit();
                    ShowSuccess($"Successfully placed {placedCount} instances.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to place families: {ex.Message}");
            }
        }

        private bool ValidateInputs()
        {
            if (SelectedFamilyType == null)
            {
                ShowError("Please select a family type.");
                return false;
            }

            if (SelectedRoom == null)
            {
                ShowError("Please select a room.");
                return false;
            }

            if (UDistance <= 0 || VDistance <= 0)
            {
                ShowError("Distance values must be greater than zero.");
                return false;
            }

            return true;
        }

        private bool IsPointInRoom(XYZ point, Room room)
        {
            // Create a temporary point to check containment
            XYZ checkPoint = new XYZ(point.X, point.Y, (room.get_BoundingBox(null).Min.Z + room.get_BoundingBox(null).Max.Z) / 2);
            return room.IsPointInRoom(checkPoint);
        }

        private void ShowError(string message)
        {
            TaskDialog.Show("Error", message);
        }

        private void ShowSuccess(string message)
        {
            TaskDialog.Show("Success", message);
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    public class RoomSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Room;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}