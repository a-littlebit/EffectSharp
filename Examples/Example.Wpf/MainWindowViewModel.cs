using CommunityToolkit.Mvvm.Input;
using EffectSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example.Wpf
{
    public class TodoItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual required string Title { get; set; }
        public virtual bool IsCompleted { get; set; }
    }

    public partial class MainWindowViewModel
    {
        public Ref<string> NewTodoTitle { get; } = Reactive.Ref(string.Empty);
        public ReactiveCollection<TodoItem> TodoItems { get; } = new();

        public Ref<bool> ShowCompletedItems { get; } = Reactive.Ref(false);
        public Ref<bool> ShowPendingItems { get; } = Reactive.Ref(false);
        public Ref<bool> ShowAllItems { get; } = Reactive.Ref(true);

        public ObservableCollection<TodoItem> FilteredTodoItems { get; } = new();

        public MainWindowViewModel()
        {
            Reactive.DiffAndBindToCollection(Reactive.Computed(() =>
            {
                return ShowAllItems.Value
                    ? TodoItems.ToList()
                    : TodoItems.Where(item => item.IsCompleted == ShowCompletedItems.Value).ToList();
            }), FilteredTodoItems);
        }

        [RelayCommand]
        public void AddTodo()
        {
            if (!string.IsNullOrWhiteSpace(NewTodoTitle.Value))
            {
                TodoItems.Add(Reactive.Create(new TodoItem { Title = NewTodoTitle.Value, IsCompleted = false }));
                NewTodoTitle.Value = string.Empty;
            }
        }

        [RelayCommand]
        public void SelectAll()
        {
            ShowAllItems.Value = true;
            ShowCompletedItems.Value = false;
            ShowPendingItems.Value = false;
        }

        [RelayCommand]
        public void SelectCompleted()
        {
            ShowAllItems.Value = false;
            ShowCompletedItems.Value = true;
            ShowPendingItems.Value = false;
        }

        [RelayCommand]
        public void SelectPending()
        {
            ShowAllItems.Value = false;
            ShowCompletedItems.Value = false;
            ShowPendingItems.Value = true;
        }
    }
}
