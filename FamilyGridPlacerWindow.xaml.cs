using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace RevitFamilyGridPlacer
{
    public partial class FamilyGridPlacerWindow : Window
    {
        private readonly FamilyGridPlacerViewModel _viewModel;

        public FamilyGridPlacerWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Create and set the view model
            _viewModel = new FamilyGridPlacerViewModel(uidoc);
            DataContext = _viewModel;

            // Set owner window to Revit's main window
            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = uidoc.Application.MainWindowHandle;

            // Make window topmost
            Topmost = true;

            // Subscribe to closing event
            Closing += OnWindowClosing;

            // Set initial button states
            UpdatePlaceButtonState();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _viewModel?.Dispose();
        }

        private void UpdatePlaceButtonState()
        {
            bool canPlace = _viewModel?.SelectedFamilyType != null &&
                           _viewModel?.SelectedRoom != null &&
                           ValidateInputs();

            btnPlaceFamilies.IsEnabled = canPlace;
        }

        private bool ValidateInputs()
        {
            if (!ValidateNumericInput(txtUDistance.Text, "U Distance", out double uDistance) || uDistance <= 0)
                return false;

            if (!ValidateNumericInput(txtVDistance.Text, "V Distance", out double vDistance) || vDistance <= 0)
                return false;

            if (!ValidateNumericInput(txtWallOffset.Text, "Wall Offset", out _))
                return false;

            if (!_viewModel.UseUpperSurface)
            {
                if (!ValidateNumericInput(txtDefaultHeight.Text, "Default Height", out double defaultHeight) || defaultHeight <= 0)
                    return false;
            }

            return true;
        }

        private bool ValidateNumericInput(string input, string fieldName, out double result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = 0;
                ShowError($"{fieldName} cannot be empty.");
                return false;
            }

            // Replace comma with dot for proper parsing
            input = input.Replace(",", ".");

            if (!double.TryParse(input, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                ShowError($"{fieldName} must be a valid number.");
                return false;
            }

            return true;
        }

        private void ShowError(string message)
        {
            TaskDialog.Show("Validation Error", message);
        }

        private void ShowSuccess(string message)
        {
            TaskDialog.Show("Success", message);
        }

        private void PlaceFamiliesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateInputs())
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    _viewModel.PlaceFamiliesCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred while placing families: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnFamilyTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlaceButtonState();
        }

        private void OnRoomChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlaceButtonState();
        }

        private void OnNumericTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceButtonState();
        }

        private void OnPlacementMethodChanged(object sender, RoutedEventArgs e)
        {
            if (txtDefaultHeight != null)
            {
                txtDefaultHeight.IsEnabled = !_viewModel.UseUpperSurface;
                lblDefaultHeight.IsEnabled = !_viewModel.UseUpperSurface;
            }
            UpdatePlaceButtonState();
        }

        private void NumericValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!(char.IsDigit(c) || c == '.' || c == '-' || c == ','))
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }

   
}