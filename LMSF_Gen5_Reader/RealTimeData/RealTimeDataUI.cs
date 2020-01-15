using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     Handles the selecting and displaying of real time data.
    ///     Selecting the data is done through a ComboBox control that is updated with all unique data set
    ///     parameter names from the <see cref="RealTimeData" /> model.
    ///     Displaying the data includes rendering a graph with labeled columns and rows, and displaying dots
    ///     that represent the data values. If only one value exists for a given row/column, then the raw value
    ///     is also drawn in the cell.
    /// </summary>
    public sealed class RealTimeDataUI
    {
        public string SelectedParameter { get; private set; } = string.Empty;

        private const int Rows = 8;
        private const int Columns = 12;
        private const string NoDataParameter = "---No data---";

        private readonly Canvas plotterUI;
        private readonly ComboBox parameterUI;
        private readonly ObservableCollection<string> parameters = new ObservableCollection<string>();

        private double cellHeight, cellWidth;
        private long allCellsWidth, allCellsHeight;
        private long cellsStartX, cellsStartY;
        private long topLegendSize, leftLegendSize;
        private readonly string[] rowLabels 
            = new string[Rows] { "A", "B", "C", "D", "E", "F", "G", "H" };
        private readonly string[] columnLabels 
            = new string[Columns] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealTimeDataUI" /> class.
        ///     Populates the class with the given <see cref="Canvas" /> and <see cref="ComboBox" />
        ///     that will show the real time data and the selecting of a data set, respectively.
        /// </summary>
        /// <param name="plotterUI">
        ///     The <see cref="Canvas" /> instance that will be used to render the real time data to.
        /// </param>
        /// <param name="parameterUI">
        ///     The <see cref="ComboBox" /> that will be used to allow the user to select a specific data set
        ///     to be rendered in the UI.
        /// </param>
        public RealTimeDataUI(Canvas plotterUI, ComboBox parameterUI)
        {
            this.plotterUI = plotterUI;
            this.parameterUI = parameterUI;
        }

        /// <summary>
        ///     Adds the labels for the rows and columns, then adds the lines that divide up the plate into wells.
        /// </summary>
        public void DrawPlotterCells()
        {
            plotterUI.Children.Clear();
            UpdateCanvasDrawingValues();

            for (var y = 0; y < Rows; y++)
            {
                var letter = new TextBlock
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                    Margin = new Thickness(1, topLegendSize + (cellHeight * y) + (cellHeight / 4), 0, 0),
                    Text = rowLabels[y]
                };

                plotterUI.Children.Add(letter);

                var line = new Line
                {
                    X1 = cellsStartX,
                    Y1 = cellsStartY + (cellHeight * y),
                    X2 = cellsStartX + allCellsWidth,
                    Y2 = cellsStartY + (cellHeight * y),
                    Stroke = Brushes.DarkSlateBlue,
                    StrokeThickness = 0.5
                };

                plotterUI.Children.Add(line);
            }

            for (var x = 0; x < Columns; x++)
            {
                var letter = new TextBlock
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                    Margin = new Thickness(leftLegendSize + (cellWidth * x) + (cellWidth / 3), -3, 0, 0),
                    Text = columnLabels[x]
                };

                plotterUI.Children.Add(letter);

                var line = new Line
                {
                    X1 = cellsStartX + cellWidth * x,
                    Y1 = cellsStartY,
                    X2 = cellsStartX + cellWidth * x,
                    Y2 = cellsStartY + allCellsHeight,
                    Stroke = Brushes.DarkSlateBlue,
                    StrokeThickness = 0.5
                };

                plotterUI.Children.Add(line);
            }
        }

        /// <summary>
        ///     Plots the data from the selected data set parameter name from the <see cref="parameterUI" />
        ///     combo box. If the selected data set name does not exist in the given real time data model,
        ///     then nothing is plotted. This condition occurs when the real time data model does not have data
        ///     yet and the selection is equal to <see cref="NoDataParameter"/> placeholder paramter name.
        /// </summary>
        /// <param name="realTimeData">
        ///     The real time data model that holds the application's current data sets.
        /// </param>
        public void PlotSelectedParameterData(RealTimeData realTimeData)
        {
            var selectedData = GetSelectedParameterData(realTimeData);

            if (selectedData == null)
            {
                return;
            }

            PlotDataSet(selectedData);
        }

        /// <summary>
        ///     Updates the current data set parameter name to the selection clicked.
        ///     Called when the <see cref="parameterUI" /> combo box is clicked.
        ///     When initially called with an empty parameter name list, populates a placeholder
        ///     <see cref="NoDataParameter" /> name.
        /// </summary>
        public void UpdateClickedSelection()
        {
            var selectedIndex = (parameterUI.SelectedIndex == -1) ? 0 : parameterUI.SelectedIndex;

            if (parameters.Count == 0)
            {
                selectedIndex = 0;
                SelectedParameter = NoDataParameter;
                parameters.Add(NoDataParameter);
                parameterUI.ItemsSource = parameters;
                parameterUI.SelectedIndex = selectedIndex;
            }

            SelectedParameter = parameters[selectedIndex];
        }

        /// <summary>
        ///     Updates the <see cref="parameters" /> list of parameter names with any new data sets found
        ///     in the real time data model.
        ///     Removes the placeholder <see cref="NoDataParameter" /> selection if adding a new data set.
        /// </summary>
        /// <param name="realTimeData">
        ///     The real time data model that holds the application's current data sets.
        /// </param>
        public void UpdateSelections(RealTimeData realTimeData)
        {
            // If given no data, updates selections to be empty
            if (realTimeData == null || realTimeData.DataSets.Count == 0)
            {
                parameters.Clear();
                UpdateClickedSelection();
                return;
            }

            foreach (var dataSet in realTimeData.DataSets)
            {
                // Add new parameter/data set
                if (parameters.FirstOrDefault(x => x.Equals(dataSet.ParameterName)) == null)
                {
                    parameters.Add(dataSet.ParameterName);

                    // Remove 'no data' if exists
                    if (parameters.FirstOrDefault(x => x.Equals(NoDataParameter)) != null)
                    {
                        parameters.Remove(NoDataParameter);

                        var selectionIndex = (parameterUI.SelectedIndex < 0) ? 0 : parameterUI.SelectedIndex;
                        SelectedParameter = parameters[selectionIndex];
                    }
                }
            }
        }

        /// <summary>
        ///     Draws a dot (ellipse) on the <see cref="plotterUI" /> at the given coordinate position.
        /// </summary>
        /// <param name="x">X position on the canvas to draw the dot.</param>
        /// <param name="y">Y position on the canvas to draw the dot.</param>
        private void DrawDotOnPlotter(long x, long y)
        {
            var dot = new Ellipse
            {
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 1.5,
                Width = 3,
                Height = 3,
                Margin = new Thickness(x, y, 0, 0)
            };

            plotterUI.Children.Add(dot);
        }

        /// <summary>
        ///     Returns the data set from the given <see cref="RealTimeData" /> that matches the currently
        ///     selected parameter name.
        /// </summary>
        /// <param name="realTimeData">
        ///     The real time data model that holds the application's current data sets.
        /// </param>
        /// <returns>
        ///     The data set from the real time data model that matches the selected parameter name, or
        ///     null if no match is found.
        /// </returns>
        private RawDataSetModel GetSelectedParameterData(RealTimeData realTimeData)
            => realTimeData.DataSets.FirstOrDefault(x => x.ParameterName.Equals(SelectedParameter));

        /// <summary>
        ///     Plots the data as dots from a given <see cref="RawDataSetModel" /> onto the
        ///     <see cref="plotterUI" /> canvas.
        /// </summary>
        /// <param name="dataSet">The data set to plot in the UI.</param>
        private void PlotDataSet(RawDataSetModel dataSet)
        {
            UpdateCanvasDrawingValues();

            var maximumValue = Double.MinValue;
            var minimumValue = Double.MaxValue;

            for (var x = 0; x < Rows; x++)
            {
                for (var y = 0; y < Columns; y++)
                {
                    foreach (var data in dataSet.RawDataPlate[x, y])
                    {
                        if (data.Value > maximumValue)
                        {
                            maximumValue = data.Value;
                        }

                        if (data.Value < minimumValue)
                        {
                            minimumValue = data.Value;
                        }
                    }
                }
            }

            var minMaxDifference = (maximumValue - minimumValue == 0) ? 0.0001 : maximumValue - minimumValue;
            var valueRatio = (cellHeight - 8) / minMaxDifference;

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var numberOfValues = dataSet.RawDataPlate[row, column].Count;

                    // No data to plot
                    if (numberOfValues == 0)
                    {
                        continue;
                    }

                    // Single data plotting
                    if (numberOfValues == 1)
                    {
                        PlotSingleDataValue(dataSet.RawDataPlate[row, column].First(), row, column, valueRatio, minimumValue);
                        continue;
                    }

                    // Multiple data plotting
                    PlotMultipleDataValues(dataSet.RawDataPlate[row, column], row, column, valueRatio, minimumValue);
                }
            }
        }

        /// <summary>
        ///     Plots the data from a given collection of <see cref="RawDataModel" /> that belong to a given
        ///     row and column.
        /// </summary>
        /// <param name="data">A collection of <see cref="RawDataModel"/> from a data set row and column.</param>
        /// <param name="row">The row that the data is to be plotted in.</param>
        /// <param name="column">The column that the data is to be plotted in.</param>
        /// <param name="yRatio">
        ///     The ratio to multiply the data's value by to position it to fit within its cell.
        /// </param>
        /// <param name="minimumValue">
        ///     The lowest value in this data set to ensure the smallest data value is always plotted at the bottom
        ///     of the cell.
        /// </param>
        private void PlotMultipleDataValues(List<RawDataModel> data, int row, int column, double yRatio, double minimumValue)
        {
            var cellX = cellsStartX + (column * cellWidth);
            var cellY = cellsStartY + (row * cellHeight);
            var innerCellX = cellX + 2;
            var innerCellY = cellY + 4;
            var innerCellWidth = cellWidth - 8;
            var innerCellHeight = cellHeight - 8;
            var innerCellSeparationWidth = (cellWidth - 8) / (data.Count - 1);
            var numberOfValues = data.Count;

            for (var i = 0; i < numberOfValues; i++)
            {
                var dotX = innerCellX + (innerCellSeparationWidth * i);
                var dotY = innerCellY + innerCellHeight - ((data[i].Value - minimumValue) * yRatio);

                DrawDotOnPlotter((long)dotX, (long)dotY);
            }
        }

        /// <summary>
        ///     Plots a single data value that belong to a given row and column.
        ///     Unlike when rendering multiple data values, the raw data value text is displayed in
        ///     the cell, and the data value is positioned in the center of the cell.
        /// </summary>
        /// <param name="data">The single data model of a given row and column in a data set.</param>
        /// <param name="row">The row that the data is to be plotted in.</param>
        /// <param name="column">The column that the data is to be plotted in.</param>
        /// <param name="yRatio">
        ///     The ratio to multiply the data's value by to position it to fit within its cell.
        /// </param>
        /// <param name="minimumValue">
        ///     The lowest value in this data set to ensure the smallest data value is always plotted at the bottom
        ///     of the cell.
        /// </param>
        private void PlotSingleDataValue(RawDataModel data, int row, int column, double yRatio, double minimumValue)
        {
            var cellX = cellsStartX + (column * cellWidth);
            var cellY = cellsStartY + (row * cellHeight);

            var valueText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                Margin = new Thickness(cellX, cellY, 0, 0),
                Text = string.Format("{0:0.00000}", data.Value)
            };

            plotterUI.Children.Add(valueText);

            var dotX = cellX + (cellWidth / 2);
            var dotY = cellY + cellHeight - ((data.Value - minimumValue) * yRatio) - 1;

            DrawDotOnPlotter((long)dotX, (long)dotY);
        }

        /// <summary>
        ///     Updates the values used when drawing the <see cref="plotterUI" /> to ensure they scale
        ///     with the window.
        /// </summary>
        private void UpdateCanvasDrawingValues()
        {
            topLegendSize = 16;
            leftLegendSize = 16;

            cellsStartX = topLegendSize;
            cellsStartY = leftLegendSize;

            allCellsWidth = Convert.ToInt64(plotterUI.Width - leftLegendSize);
            allCellsHeight = Convert.ToInt64(plotterUI.Height - topLegendSize);

            cellWidth = (double)allCellsWidth / Columns;
            cellHeight = (double)allCellsHeight / Rows;
        }
    }
}