using Autodesk.Revit.UI;
using System;
using System.Windows.Media.Imaging;

namespace RevitFamilyGridPlacer
{
    public class Application : IExternalApplication
    {
        private const string TabName = "Autotech";
        private const string PanelName = "Family Tools";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create ribbon tab
                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch { }

                // Create ribbon panel
                RibbonPanel panel = null;
                try
                {
                    panel = application.CreateRibbonPanel(TabName, PanelName);
                }
                catch
                {
                    panel = application.GetRibbonPanels(TabName)
                        .Find(p => p.Name == PanelName);
                }

                // Get assembly location
                string assemblyPath = typeof(Application).Assembly.Location;

                // Create push button data
                PushButtonData buttonData = new PushButtonData(
                    "FamilyGridPlacer",
                    "Place\nFamilies",
                    assemblyPath,
                    "RevitFamilyGridPlacer.FamilyGridPlacerCommand")
                {
                    ToolTip = "Place families in a grid pattern within rooms"
                };

                // Set button images
                buttonData.LargeImage = GetResourceImage("RevitFamilyGridPlacer.Resources.grid32.png");
                buttonData.Image = GetResourceImage("RevitFamilyGridPlacer.Resources.grid16.png");

                // Add button to panel
                panel.AddItem(buttonData);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        private BitmapSource GetResourceImage(string resourcePath)
        {
            try
            {
                var stream = this.GetType().Assembly.GetManifestResourceStream(resourcePath);
                if (stream != null)
                {
                    var decoder = new PngBitmapDecoder(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.Default);
                    return decoder.Frames[0];
                }
            }
            catch { }
            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}