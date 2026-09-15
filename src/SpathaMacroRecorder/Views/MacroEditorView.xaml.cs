using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SpathaMacroRecorder.Models;
using SpathaMacroRecorder.ViewModels;

namespace SpathaMacroRecorder.Views;

public partial class MacroEditorView : UserControl
{
    public MacroEditorView()
    {
        InitializeComponent();

        // Номера строк живут в DataGridRow.Header. Вставка и удаление сдвигают всё, что ниже,
        // поэтому после каждого изменения список номеров пересчитывается целиком.
        ((INotifyCollectionChanged)StepsGrid.Items).CollectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(RenumberRows, DispatcherPriority.Loaded);
    }

    private void StepsGrid_LoadingRow(object sender, DataGridRowEventArgs e) =>
        e.Row.Header = e.Row.GetIndex() + 1;

    /// <summary>
    /// Строки в DataGrid переиспользуются при прокрутке и вставке, и старый номер уезжает
    /// вместе с ними — отсюда были подряд идущие строки с номерами вроде 12, 14, 9.
    /// Обходим только созданные контейнеры: остальным номер проставит LoadingRow.
    /// </summary>
    private void RenumberRows()
    {
        for (int i = 0; i < StepsGrid.Items.Count; i++)
        {
            if (StepsGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
            {
                row.Header = i + 1;
            }
        }
    }

    private MacroEditorViewModel? ViewModel => DataContext as MacroEditorViewModel;

    private void StepsGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        // DataGrid коммитит правку задержки после выхода из ячейки — сохраняем профиль сразу же,
        // отдельной кнопки "Сохранить" в макете нет.
        ViewModel?.SaveCurrentProfile();
    }

    private void MacrosList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel?.SetSelectedMacros(MacrosList.SelectedItems.OfType<Macro>());

    private void StepsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel?.SetSelectedSteps(StepsGrid.SelectedItems.OfType<MacroStep>());

    /// <summary>
    /// Delete удаляет выделенные шаги. Если в этот момент правится ячейка задержки, клавиша
    /// должна стирать текст — такие нажатия пропускаем: их источник TextBox, а не таблица.
    /// </summary>
    private void StepsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || e.OriginalSource is TextBox)
        {
            return;
        }

        if (ViewModel?.DeleteStepCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void MacrosList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || e.OriginalSource is TextBox)
        {
            return;
        }

        if (ViewModel?.DeleteMacroCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void SelectAllSteps_Click(object sender, RoutedEventArgs e)
    {
        StepsGrid.SelectAll();
        StepsGrid.Focus();
    }

    private void SelectAllMacros_Click(object sender, RoutedEventArgs e)
    {
        MacrosList.SelectAll();
        MacrosList.Focus();
    }
}
