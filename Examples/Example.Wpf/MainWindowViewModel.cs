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
    public interface ITodoItem
    {
        [ReactiveProperty(reactive: false)]
        Guid Id { get; set; }

        string Title { get; set; }

        [ReactiveProperty(defaultValue: false)]
        bool IsCompleted { get; set; }
    }

    public partial class MainWindowViewModel
    {
        public Ref<string> NewTodoTitle { get; } = Reactive.Ref(string.Empty);
        public ReactiveCollection<ITodoItem> TodoItems { get; } = new();

        public Ref<bool> ShowCompletedItems { get; } = Reactive.Ref(false);
        public Ref<bool> ShowPendingItems { get; } = Reactive.Ref(false);
        public Ref<bool> ShowAllItems { get; } = Reactive.Ref(true);

        public ObservableCollection<ITodoItem> FilteredTodoItems { get; }

        public MainWindowViewModel()
        {
            FilteredTodoItems = Reactive.ComputedList<ITodoItem, List<ITodoItem>, Guid>(() =>
            {
                var toShow = ShowAllItems.Value
                    ? TodoItems
                    : TodoItems.Where(item => item.IsCompleted == ShowCompletedItems.Value);

                return toShow.OrderBy(item => item.IsCompleted).ThenBy(item => item.Title).ToList();
            }, item => item.Id);

            AddTodoCommand = FunctionCommand.Create<object>(_ => AddTodo(), () => !string.IsNullOrWhiteSpace(NewTodoTitle.Value));
            SelectAllCommand = FunctionCommand.Create<object>(_ => SelectAll());
            SelectCompletedCommand = FunctionCommand.Create<object>(_ => SelectCompleted());
            SelectPendingCommand = FunctionCommand.Create<object>(_ => SelectPending());
        }

        public IFunctionCommand<object> AddTodoCommand { get; }

        public void AddTodo()
        {
            if (!string.IsNullOrWhiteSpace(NewTodoTitle.Value))
            {
                var item = Reactive.Create<ITodoItem>();
                item.Id = Guid.NewGuid();
                item.Title = NewTodoTitle.Value;
                TodoItems.Add(item);
                NewTodoTitle.Value = string.Empty;
            }
        }

        public IFunctionCommand<object> SelectAllCommand { get; }

        public void SelectAll()
        {
            ShowAllItems.Value = true;
            ShowCompletedItems.Value = false;
            ShowPendingItems.Value = false;
        }

        public IFunctionCommand<object> SelectCompletedCommand { get; }

        public void SelectCompleted()
        {
            ShowAllItems.Value = false;
            ShowCompletedItems.Value = true;
            ShowPendingItems.Value = false;
        }

        public IFunctionCommand<object> SelectPendingCommand { get; }

        public void SelectPending()
        {
            ShowAllItems.Value = false;
            ShowCompletedItems.Value = false;
            ShowPendingItems.Value = true;
        }
    }
}
