using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkGeo.MapSuite.Drawing;
using ThinkGeo.MapSuite.Layers;
using ThinkGeo.MapSuite.Shapes;
using ThinkGeo.MapSuite.Styles;
using ThinkGeo.MapSuite.Wpf;
using ThinkGeo.MapSuite.WorldReverseGeocoding;
using ThinkGeo.MapSuite;

namespace PlaceSearchWorldReverseGeocoding
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OsmReverseGeocoder osmReverseGeocoder;
        private PlaceReverseGeocoderResult searchResult;
        private Collection<Place> serachedPlaces;

        private PointShape searchPoint;
        private SimpleMarkerOverlay bestmatchingMarkerOverlay;
        private SimpleMarkerOverlay nearbysMarkerOverlay;
        private InMemoryFeatureLayer seachRadiusFeatureLayer;
        private InMemoryFeatureLayer resultGeometryFeatureLayer;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize the osmReverseGeocoder with the testing SQLiteDatabase.
            osmReverseGeocoder = new OsmReverseGeocoder(ConfigurationManager.ConnectionStrings["SQLiteConnectionString"].ConnectionString);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            wpfMap.MapUnit = GeographyUnit.DecimalDegree;

            // Add background map.
            WorldStreetsAndImageryOverlay baseOverlay = new WorldStreetsAndImageryOverlay();
            baseOverlay.Projection = WorldStreetsAndImageryProjection.DecimalDegrees;
            wpfMap.Overlays.Add(baseOverlay);

            // Add marker overlay for showing the best matching place.
            bestmatchingMarkerOverlay = new SimpleMarkerOverlay();
            wpfMap.Overlays.Add("BestMatchingMarkerOverlay", bestmatchingMarkerOverlay);

            // Add marker overlay for showing result.
            nearbysMarkerOverlay = new SimpleMarkerOverlay();
            wpfMap.Overlays.Add("NearbysMarkerOverlay", nearbysMarkerOverlay);

            // Add layer for searchRadius.
            seachRadiusFeatureLayer = new InMemoryFeatureLayer();
            seachRadiusFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultAreaStyle = new AreaStyle(new GeoPen(new GeoColor(100, GeoColors.Blue)), new GeoSolidBrush(new GeoColor(10, GeoColors.Blue)));
            seachRadiusFeatureLayer.ZoomLevelSet.ZoomLevel01.ApplyUntilZoomLevel = ApplyUntilZoomLevel.Level20;

            // Add layer for showing geometry in result.
            resultGeometryFeatureLayer = new InMemoryFeatureLayer();
            resultGeometryFeatureLayer.ZoomLevelSet.ZoomLevel01.DefaultAreaStyle = new AreaStyle(GeoPens.Blue, new GeoSolidBrush(new GeoColor(10, GeoColors.Blue)));
            resultGeometryFeatureLayer.ZoomLevelSet.ZoomLevel01.ApplyUntilZoomLevel = ApplyUntilZoomLevel.Level20;

            LayerOverlay searchResultOverlay = new LayerOverlay() { DrawingQuality = DrawingQuality.HighQuality };
            searchResultOverlay.Layers.Add("ResultGeometryFeatureLayer", resultGeometryFeatureLayer);
            searchResultOverlay.Layers.Add("SerachRadiusFeatureLayer", seachRadiusFeatureLayer);
            wpfMap.Overlays.Add("SearchResult", searchResultOverlay);

            wpfMap.CurrentExtent = new RectangleShape(-96.92379, 33.22117, -96.73, 33.07745);
            wpfMap.Refresh();

            // Bind search categories to comboxList.
            nearbySearchCategory.ItemsSource = new List<string>() { "None", "Common", "All", "Customized" };
            nearbySearchCategory.SelectedIndex = 2;
        }

        private void SearchPlaceAndNearbys()
        {
            // Reset UI and clear existing before search again.
            ClearupPreviousSearchResult();

            searchPoint = GetSearchPoint();

            // Create the searchPreference by UI controls.
            SearchPreference searchPreference = GetSearchPreferenceFromUI();
            searchResult = osmReverseGeocoder.Search(searchPoint, searchPreference);
            if (searchResult != null)
            {
                // Update search result to map markers.
                DisplaySearchResult(searchResult);

                // Update search result to left panel list.
                tabSearchResult.SelectedIndex = 0;
                TabSearchResult_SelectionChanged(tabSearchResult, null);
            }
        }

        private void nearbySearchCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (nearbySearchCategory.SelectedIndex == 0 || nearbySearchCategory.SelectedIndex == 1 || nearbySearchCategory.SelectedIndex == 2)
            {
                searchCategoriesPanel.Visibility = Visibility.Collapsed;
                SearchPlaceAndNearbys();
            }
            else
            {
                searchCategoriesPanel.Visibility = Visibility.Visible;
            }
        }

        private void TabSearchResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (searchResult != null)
            {
                // Clear the result layer,radius layer, markers and add geometry of best matching place.
                nearbysMarkerOverlay.Markers.Clear();
                resultGeometryFeatureLayer.InternalFeatures.Clear();
                seachRadiusFeatureLayer.InternalFeatures.Clear();

                if (tabSearchResult.SelectedIndex == 0) // Nearby Addresses Tab 
                {
                    AddSearchRadius(Convert.ToDouble(searchRadius.Text, CultureInfo.InvariantCulture));

                    for (int i = 0; i < searchResult.Addresses.Count; i++)
                    {
                        AddMarkerToMap(searchResult.Addresses[i], i);
                    }
                }
                else if (tabSearchResult.SelectedIndex == 1) // Nearby Intersections Tab
                {
                    AddSearchRadius(Convert.ToDouble(searchIntersectionRadius.Text, CultureInfo.InvariantCulture));

                    for (int i = 0; i < searchResult.Intersections.Count; i++)
                    {
                        AddMarkerToMap(searchResult.Intersections[i], i);
                    }
                }
                else if (tabSearchResult.SelectedIndex == 2) // Nearby Places Tab
                {
                    AddSearchRadius(Convert.ToDouble(searchRadius.Text, CultureInfo.InvariantCulture));

                    for (int i = 0; i < serachedPlaces.Count; i++)
                    {
                        AddMarkerToMap(serachedPlaces[i], i);
                    }
                }
                wpfMap.CurrentExtent = seachRadiusFeatureLayer.GetBoundingBox();
                wpfMap.Refresh();
            }
        }

        private void DisplaySearchResult(PlaceReverseGeocoderResult searchResult)
        {
            if (searchResult != null && searchResult.BestMatchingPlace != null)
            {
                // Display address of the BestMatchingPlace in the left panel and add a marker.
                txtBestMatchingPlace.Text = searchResult.BestMatchingPlace.Address;
                Marker marker = CreateMarkerByCategory("BestMatchingPlace", searchPoint, searchResult.BestMatchingPlace.Address);
                bestmatchingMarkerOverlay.Markers.Add(marker);

                // Add index,distance and direction for addresses, places and intersections.
                for (int i = 0; i < searchResult.Intersections.Count; i++)
                {
                    AddPropertiesForPlace(searchResult.Intersections[i], i);
                }
                for (int i = 0; i < searchResult.Addresses.Count; i++)
                {
                    AddPropertiesForPlace(searchResult.Addresses[i], i);
                }

                int placeIndex = 0;
                foreach (Place place in searchResult.Places)
                {
                    if (place.PlaceCategory != PlaceCategory.Highway && place.PlaceCategory != PlaceCategory.Road && place.PlaceCategory != PlaceCategory.Path && place.PlaceCategory != PlaceCategory.LinkRoad)
                    {
                        serachedPlaces.Add(place);
                        AddPropertiesForPlace(place, placeIndex);
                        placeIndex++;
                    }
                }

                // Bind addresses,intersections,places to listbox.
                lsbAddress.ItemsSource = searchResult.Addresses;
                lsbPlaces.ItemsSource = serachedPlaces;
                lsbIntersection.ItemsSource = searchResult.Intersections;
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchPlaceAndNearbys();
        }

        private void Addresses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsbAddress != null && lsbAddress.Items.Count > 0)
            {
                wpfMap.CurrentExtent = ((Place)lsbAddress.SelectedItem).Geometry.GetBoundingBox();
                wpfMap.Refresh();
                e.Handled = true;
            }
        }

        private void Intersection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsbIntersection != null && lsbIntersection.Items.Count > 0)
            {
                wpfMap.CurrentExtent = ((Intersection)lsbIntersection.SelectedItem).Location.GetBoundingBox();
                wpfMap.Refresh();
                e.Handled = true;
            }
        }

        private void Places_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lsbPlaces != null && lsbPlaces.Items.Count > 0)
            {
                wpfMap.CurrentExtent = ((Place)lsbPlaces.SelectedItem).Geometry.GetBoundingBox();
                wpfMap.Refresh();
                e.Handled = true;
            }
        }

        private Marker CreateMarkerByCategory(string category, PointShape pointShape, string tooltip)
        {
            Marker marker = null;
            // The icon of place is determined by category.
            string imageUri = string.Format("/Resources/{0}.png", category);
            marker = new Marker(pointShape)
            {
                Width = 24,
                Height = 24,
                ImageSource = new BitmapImage(new Uri(imageUri, UriKind.RelativeOrAbsolute)),
                ToolTip = tooltip
            };

            if (category == "BestMatchingPlace")
            {
                marker.Width = 32;
                marker.Height = 32;
                marker.YOffset = -10;
            }

            return marker;
        }

        private Marker GetMarkerByPlaceRecord(Place place)
        {
            PointShape pointShape = null;
            if (place.Properties.ContainsKey("CenterLongitude") && place.Properties.ContainsKey("CenterLatitude"))
            {
                pointShape = new PointShape(Convert.ToDouble(place.Properties["CenterLongitude"], CultureInfo.InvariantCulture), Convert.ToDouble(place.Properties["CenterLatitude"], CultureInfo.InvariantCulture));
            }
            else
            {
                pointShape = place.Geometry.GetCenterPoint();
            }

            return CreateMarkerByCategory(place.PlaceCategory.ToString().ToLower(), pointShape, string.Empty);
        }

        private SearchPreference GetSearchPreferenceFromUI()
        {
            // Set parameters for searchPreference.
            SearchPreference searchPreference = new SearchPreference()
            {
                PlaceSearchRadiusInMeter = Convert.ToDouble(searchRadius.Text, CultureInfo.InvariantCulture),
                IntersectionSearchRadiusInMeter = Convert.ToDouble(searchIntersectionRadius.Text, CultureInfo.InvariantCulture),
                IncludedIntersection = true,
                MaxResultCount = 50
            };

            // Read the search categories for nearbys.
            if (nearbySearchCategory.SelectedIndex <= 2)
            {
                searchPreference.NearbyPlaceCategory = (PlaceCategory)nearbySearchCategory.SelectedIndex;
            }
            else
            {
                Collection<PlaceCategory> searchPlaceCategoriesForNearBy = new Collection<PlaceCategory>();
                for (int i = 0; i < categoryGrid.Children.Count; i++)
                {
                    if (categoryGrid.Children[i] is CheckBox)
                    {
                        CheckBox category = (CheckBox)categoryGrid.Children[i];
                        if (category.IsChecked == true)
                        {
                            PlaceCategory selectedCategory = (PlaceCategory)(Convert.ToInt32(category.Tag, CultureInfo.InvariantCulture));
                            searchPreference.NearbyPlaceCategory = searchPreference.NearbyPlaceCategory | selectedCategory;
                        }
                    }
                }
            }
            return searchPreference;
        }

        private PointShape GetSearchPoint()
        {
            string[] coordinate = txtCoordinate.Text.Split(',');
            double lat = Convert.ToDouble(coordinate[0].Trim(), CultureInfo.InvariantCulture);
            double lon = Convert.ToDouble(coordinate[1].Trim(), CultureInfo.InvariantCulture);

            return new PointShape(lon, lat);
        }

        private void ClearupPreviousSearchResult()
        {
            // Clear existing before doing search.
            searchResult = null;
            lsbPlaces.ItemsSource = null;
            lsbAddress.ItemsSource = null;
            lsbIntersection.ItemsSource = null;
            serachedPlaces = new Collection<Place>();
            bestmatchingMarkerOverlay.Markers.Clear();
        }

        private void wpfMap_MapClick(object sender, MapClickWpfMapEventArgs e)
        {
            if (e.MouseButton == MapMouseButton.Right)
            {
                // Set the searchPoint value to coordinat textbox.
                txtCoordinate.Text = string.Format("{0},{1}", e.WorldY.ToString("0.000000"), e.WorldX.ToString("0.000000"));
                SearchPlaceAndNearbys();
            }
        }

        private void AddMarkerToMap(object reverseGeocoderRecord, int index)
        {
            Marker marker = null;
            if (reverseGeocoderRecord is Place)
            {
                marker = GetMarkerByPlaceRecord(reverseGeocoderRecord as Place);
                marker.ToolTip = (reverseGeocoderRecord as Place).Address;
                resultGeometryFeatureLayer.InternalFeatures.Add(new Feature((reverseGeocoderRecord as Place).Geometry));
            }
            else
            {
                marker = CreateMarkerByCategory("intersection", (reverseGeocoderRecord as Intersection).Location, (reverseGeocoderRecord as Intersection).Address);
            }
            marker.FontSize = 15;
            marker.Content = new TextBlock()
            {
                Text = (index + 1).ToString(CultureInfo.InvariantCulture),
                Margin = new Thickness(0, 0, 0, 35),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.Green
            };
            nearbysMarkerOverlay.Markers.Add(marker);
        }

        private void AddSearchRadius(double searchDistanceInMeters)
        {
            double searchDistance = DecimalDegreesHelper.GetLongitudeDifferenceFromDistance(searchDistanceInMeters, DistanceUnit.Meter, searchPoint.Y);
            seachRadiusFeatureLayer.InternalFeatures.Add(new Feature(new EllipseShape(searchPoint, searchDistance)));
        }

        private void AddPropertiesForPlace(object reverseGeocoderRecord, int index)
        {
            if (reverseGeocoderRecord is Place)
            {
                int distance = (int)searchPoint.GetDistanceTo((reverseGeocoderRecord as Place).Geometry, GeographyUnit.DecimalDegree, DistanceUnit.Meter);

                (reverseGeocoderRecord as Place).Properties.Add("index", (index + 1).ToString());
                (reverseGeocoderRecord as Place).Properties.Add("distance", distance.ToString());
                (reverseGeocoderRecord as Place).Properties.Add("direction", GetDirectionBetweenTwoPoints(searchPoint, (reverseGeocoderRecord as Place).Geometry.GetCenterPoint()));
            }
            else
            {
                (reverseGeocoderRecord as Intersection).OptionalNames.Add("index", (index + 1).ToString());
                (reverseGeocoderRecord as Intersection).OptionalNames.Add("distance", (reverseGeocoderRecord as Intersection).GetDistanceTo(searchPoint, DistanceUnit.Meter).ToString());
                (reverseGeocoderRecord as Intersection).OptionalNames.Add("direction", (reverseGeocoderRecord as Intersection).GetDirectionFrom(searchPoint).ToString());
            }
        }

        private string GetDirectionBetweenTwoPoints(PointShape firstPoint, PointShape secondPoint)
        {
            string southNorthDirection, eastWestDirection, direction = string.Empty;
            double slope = (firstPoint.Y - secondPoint.Y) / (firstPoint.X - secondPoint.X);

            if (firstPoint.Y > secondPoint.Y)
            {
                southNorthDirection = "South";
            }
            else
            {
                southNorthDirection = "North";
            }
            if (firstPoint.X > secondPoint.X)
            {
                eastWestDirection = "West";
            }
            else
            {
                eastWestDirection = "East";
            }

            // If the slope is greater than 3, it think the direction is south or north.
            if (Math.Abs(slope) > 3)
            {
                direction = southNorthDirection;
            }
            // If the slope is less than 0.33, it think the direction is east or west.
            else if (Math.Abs(slope) < 0.33)
            {
                direction = eastWestDirection;
            }
            // If the slope is greater than 0.3 and less than 3, it will take the corresponding direction.
            else
            {
                direction = southNorthDirection + eastWestDirection.ToLower();
            }

            return direction;
        }

    }
}