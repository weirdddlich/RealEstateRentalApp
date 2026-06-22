using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using RealEstateRental.Data;
using RealEstateRental.Models;
using RealEstateRental.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace RealEstateRental
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly AppDbContext _context = new AppDbContext();

        public ObservableCollection<Property> Properties { get; } = new ObservableCollection<Property>();
        public ObservableCollection<Tenant> Tenants { get; } = new ObservableCollection<Tenant>();
        public ObservableCollection<Contract> Contracts { get; } = new ObservableCollection<Contract>();
        public ObservableCollection<Payment> Payments { get; } = new ObservableCollection<Payment>();

        private readonly ICollectionView _tenantsView;
        private readonly ICollectionView _propertiesView;
        private readonly ICollectionView _contractsView;
        private readonly ICollectionView _paymentsView;

        public ICollectionView TenantsView => _tenantsView;
        public ICollectionView PropertiesView => _propertiesView;
        public ICollectionView ContractsView => _contractsView;
        public ICollectionView PaymentsView => _paymentsView;

        public bool IsLandlord => App.Auth.IsLandlord;

        private string _contractSearchText = string.Empty;
        public string ContractSearchText
        {
            get => _contractSearchText;
            set
            {
                if (_contractSearchText == value) return;
                _contractSearchText = value ?? string.Empty;
                OnPropertyChanged();
                _contractsView.Refresh();
            }
        }

        private string _paymentSearchText = string.Empty;
        public string PaymentSearchText
        {
            get => _paymentSearchText;
            set
            {
                if (_paymentSearchText == value) return;
                _paymentSearchText = value ?? string.Empty;
                OnPropertyChanged();
                _paymentsView.Refresh();
            }
        }

        private string _tenantSearchText = string.Empty;
        public string TenantSearchText
        {
            get => _tenantSearchText;
            set
            {
                if (_tenantSearchText == value) return;
                _tenantSearchText = value ?? string.Empty;
                OnPropertyChanged();
                _tenantsView.Refresh();
            }
        }

        private string _propertySearchText = string.Empty;
        public string PropertySearchText
        {
            get => _propertySearchText;
            set
            {
                if (_propertySearchText == value) return;
                _propertySearchText = value ?? string.Empty;
                OnPropertyChanged();
                _propertiesView.Refresh();

            }
        }

        private string _contractDebtMinText = string.Empty;
        public string ContractDebtMinText
        {
            get => _contractDebtMinText;
            set
            {
                if (_contractDebtMinText == value) return;
                _contractDebtMinText = value ?? string.Empty;
                OnPropertyChanged();
                _contractsView.Refresh();
            }
        }

        private string _contractDebtMaxText = string.Empty;
        public string ContractDebtMaxText
        {
            get => _contractDebtMaxText;
            set
            {
                if (_contractDebtMaxText == value) return;
                _contractDebtMaxText = value ?? string.Empty;
                OnPropertyChanged();
                _contractsView.Refresh();
            }
        }

        private string _contractDebtStatusFilterValue = "All";
        public string ContractDebtStatusFilterValue
        {
            get => _contractDebtStatusFilterValue;
            set
            {
                if (_contractDebtStatusFilterValue == value) return;
                _contractDebtStatusFilterValue = value ?? "All";
                OnPropertyChanged();
                _contractsView.Refresh();
            }
        }

        public Visibility UnpaidNotificationVisibility
        {
            get => _unpaidNotificationVisibility;
            private set
            {
                if (_unpaidNotificationVisibility == value) return;
                _unpaidNotificationVisibility = value;
                OnPropertyChanged();
            }
        }
        private Visibility _unpaidNotificationVisibility = Visibility.Collapsed;

        private string _unpaidNotificationText = string.Empty;
        public string UnpaidNotificationText
        {
            get => _unpaidNotificationText;
            private set
            {
                if (_unpaidNotificationText == value) return;
                _unpaidNotificationText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ContractScheduleItem> ContractScheduleItems { get; } = new ObservableCollection<ContractScheduleItem>();

        private readonly DispatcherTimer _notificationTimer = new DispatcherTimer();
        private int _lastUnpaidContractsCount = -1;
        private bool _closingForRelogin;
        private static readonly HashSet<string> AllowedContractFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx"
        };

        private Property? _selectedProperty;
        public Property? SelectedProperty
        {
            get => _selectedProperty;
            set
            {
                _selectedProperty = value;
                OnPropertyChanged();
                if (value != null)
                {
                    PropertyNameInput = value.Name;
                    PropertyPriceInput = value.PricePerMonth.ToString();
                }
            }
        }

        private Tenant? _selectedTenant;
        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set
            {
                _selectedTenant = value;
                OnPropertyChanged();
                if (value != null)
                {
                    TenantNameInput = value.Name;
                }
            }
        }

        private Contract? _selectedContract;
        public Contract? SelectedContract
        {
            get => _selectedContract;
            set
            {
                _selectedContract = value;
                OnPropertyChanged();
                if (value != null)
                {
                    SelectedTenantForContract = value.Tenant;
                    SelectedPropertyForContract = value.Property;
                    ContractStartDateInput = value.StartDate;
                    ContractEndDateInput = value.EndDate;
                }

                GenerateScheduleForSelectedContract();
            }
        }

        private Payment? _selectedPayment;
        public Payment? SelectedPayment
        {
            get => _selectedPayment;
            set
            {
                _selectedPayment = value;
                OnPropertyChanged();
                if (value != null)
                {
                    SelectedContractForPayment = value.Contract;
                    PaymentDateInput = value.PaymentDate;
                    PaymentAmountInput = value.Amount.ToString();
                }
            }
        }

        public string PropertyNameInput
        {
            get => _propertyNameInput;
            set { _propertyNameInput = value; OnPropertyChanged(); }
        }
        private string _propertyNameInput = string.Empty;

        public string PropertyPriceInput
        {
            get => _propertyPriceInput;
            set { _propertyPriceInput = value; OnPropertyChanged(); }
        }
        private string _propertyPriceInput = string.Empty;

        public string TenantNameInput
        {
            get => _tenantNameInput;
            set { _tenantNameInput = value; OnPropertyChanged(); }
        }
        private string _tenantNameInput = string.Empty;

        public Tenant? SelectedTenantForContract
        {
            get => _selectedTenantForContract;
            set { _selectedTenantForContract = value; OnPropertyChanged(); }
        }
        private Tenant? _selectedTenantForContract;

        public Property? SelectedPropertyForContract
        {
            get => _selectedPropertyForContract;
            set
            {
                _selectedPropertyForContract = value;
                OnPropertyChanged();
                UpdateContractMonthlyRentDisplay();
            }
        }
        private Property? _selectedPropertyForContract;

        public DateTime? ContractStartDateInput
        {
            get => _contractStartDateInput;
            set { _contractStartDateInput = value; OnPropertyChanged(); }
        }
        private DateTime? _contractStartDateInput = DateTime.Today;

        public DateTime? ContractEndDateInput
        {
            get => _contractEndDateInput;
            set { _contractEndDateInput = value; OnPropertyChanged(); }
        }
        private DateTime? _contractEndDateInput;

        public string ContractMonthlyRentDisplay
        {
            get => _contractMonthlyRentDisplay;
            private set { _contractMonthlyRentDisplay = value; OnPropertyChanged(); }
        }
        private string _contractMonthlyRentDisplay = string.Empty;

        private void UpdateContractMonthlyRentDisplay()
        {
            if (SelectedPropertyForContract != null)
                ContractMonthlyRentDisplay = SelectedPropertyForContract.PricePerMonth.ToString(CultureInfo.CurrentCulture);
            else
                ContractMonthlyRentDisplay = string.Empty;
        }

        public Contract? SelectedContractForPayment
        {
            get => _selectedContractForPayment;
            set { _selectedContractForPayment = value; OnPropertyChanged(); }
        }
        private Contract? _selectedContractForPayment;

        public DateTime? PaymentDateInput
        {
            get => _paymentDateInput;
            set { _paymentDateInput = value; OnPropertyChanged(); }
        }
        private DateTime? _paymentDateInput = DateTime.Today;

        public string PaymentAmountInput
        {
            get => _paymentAmountInput;
            set { _paymentAmountInput = value; OnPropertyChanged(); }
        }
        private string _paymentAmountInput = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            Title = $"{Title} — {App.Auth.Username} ({(IsLandlord ? "Арендодатель" : "Арендатор")})";

            _tenantsView = CollectionViewSource.GetDefaultView(Tenants);
            _tenantsView.Filter = TenantFilterPredicate;

            _propertiesView = CollectionViewSource.GetDefaultView(Properties);
            _propertiesView.Filter = PropertyFilterPredicate;

            _contractsView = CollectionViewSource.GetDefaultView(Contracts);
            _contractsView.Filter = ContractDebtFilterPredicate;

            _paymentsView = CollectionViewSource.GetDefaultView(Payments);
            _paymentsView.Filter = PaymentFilterPredicate;

            DataContext = this;
            LoadData();

            UpdateUnpaidNotificationAndAnalyticsAndSchedule();

            _lastUnpaidContractsCount = GetUnpaidContractsCount();
            if (_lastUnpaidContractsCount > 0)
            {
                MessageBox.Show(
                    $"{_lastUnpaidContractsCount} договор(ов) имеют задолженность.",
                    "Договоры с задолженностью",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            StartNotificationTimer();
        }

        private void LoadData()
        {
            Properties.Clear();
            Tenants.Clear();
            if (IsLandlord)
            {
                foreach (var p in _context.Properties.OrderBy(p => p.Name))
                    Properties.Add(p);

                foreach (var t in _context.Tenants.OrderBy(t => t.Name))
                    Tenants.Add(t);
            }

            Contracts.Clear();
            var contractsQuery = _context.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Property)
                .Include(c => c.Payments)
                .AsQueryable();

            if (!IsLandlord && App.Auth.TenantId is int tenantId)
                contractsQuery = contractsQuery.Where(c => c.TenantId == tenantId);

            foreach (var c in contractsQuery.OrderBy(c => c.Id).ToList())
                Contracts.Add(c);

            Payments.Clear();
            var paymentsQuery = _context.Payments
                .Include(p => p.Contract)!.ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Property)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Payments)
                .AsQueryable();

            if (!IsLandlord && App.Auth.TenantId is int tid)
                paymentsQuery = paymentsQuery.Where(p => p.Contract!.TenantId == tid);

            foreach (var p in paymentsQuery.OrderBy(p => p.PaymentDate).ToList())
                Payments.Add(p);

            _tenantsView.Refresh();
            _propertiesView.Refresh();
            _contractsView.Refresh();
            _paymentsView.Refresh();

            UpdateUnpaidNotificationAndAnalyticsAndSchedule();
        }

        private void RefreshContractsAndPayments()
        {
            var selectedContractId = SelectedContract?.Id;
            var selectedPaymentId = SelectedPayment?.Id;

            Contracts.Clear();
            var contractsQuery = _context.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Property)
                .Include(c => c.Payments)
                .AsQueryable();

            if (!IsLandlord && App.Auth.TenantId is int tenantId)
                contractsQuery = contractsQuery.Where(c => c.TenantId == tenantId);

            foreach (var c in contractsQuery.OrderBy(c => c.Id).ToList())
                Contracts.Add(c);

            Payments.Clear();
            var paymentsQuery = _context.Payments
                .Include(p => p.Contract)!.ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Property)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Payments)
                .AsQueryable();

            if (!IsLandlord && App.Auth.TenantId is int tid)
                paymentsQuery = paymentsQuery.Where(p => p.Contract!.TenantId == tid);

            foreach (var p in paymentsQuery.OrderBy(p => p.PaymentDate).ToList())
                Payments.Add(p);

            if (selectedContractId.HasValue)
                SelectedContract = Contracts.FirstOrDefault(c => c.Id == selectedContractId.Value);

            if (selectedPaymentId.HasValue)
                SelectedPayment = Payments.FirstOrDefault(p => p.Id == selectedPaymentId.Value);

            _contractsView.Refresh();
            _paymentsView.Refresh();
            UpdateUnpaidNotificationAndAnalyticsAndSchedule();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool TenantFilterPredicate(object obj)
        {
            if (obj is not Tenant tenant)
                return false;

            var term = TenantSearchText?.Trim() ?? string.Empty;
            if (term.Length == 0)
                return true;

            return tenant.Name?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool PropertyFilterPredicate(object obj)
        {
            if (obj is not Property property)
                return false;

            var term = PropertySearchText?.Trim() ?? string.Empty;
            if (term.Length == 0)
                return true;

            if (!string.IsNullOrWhiteSpace(property.Name) &&
                property.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Allow searching by rent (price per month) as a string.
            var rentCurrentCulture = property.PricePerMonth.ToString(CultureInfo.CurrentCulture);
            var rentInvariant = property.PricePerMonth.ToString(CultureInfo.InvariantCulture);
            return rentCurrentCulture.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   rentInvariant.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ContractDebtFilterPredicate(object obj)
        {
            if (obj is not Contract contract)
                return false;

            var term = ContractSearchText?.Trim() ?? string.Empty;
            if (term.Length > 0)
            {
                var tenant = contract.Tenant?.Name ?? string.Empty;
                var prop = contract.Property?.Name ?? string.Empty;
                var rentCur = contract.MonthlyRent.ToString(CultureInfo.CurrentCulture);
                var rentInv = contract.MonthlyRent.ToString(CultureInfo.InvariantCulture);
                var matches =
                    tenant.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    prop.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rentCur.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rentInv.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches)
                    return false;
            }

            var debt = contract.Debt;

            decimal? minDebt = null;
            decimal? maxDebt = null;

            if (!string.IsNullOrWhiteSpace(ContractDebtMinText) &&
                decimal.TryParse(ContractDebtMinText, NumberStyles.Number, CultureInfo.CurrentCulture, out var minParsed))
                minDebt = minParsed;

            if (!string.IsNullOrWhiteSpace(ContractDebtMaxText) &&
                decimal.TryParse(ContractDebtMaxText, NumberStyles.Number, CultureInfo.CurrentCulture, out var maxParsed))
                maxDebt = maxParsed;

            if (minDebt.HasValue && debt < minDebt.Value)
                return false;
            if (maxDebt.HasValue && debt > maxDebt.Value)
                return false;

            if (!string.IsNullOrWhiteSpace(ContractDebtStatusFilterValue) &&
                !string.Equals(ContractDebtStatusFilterValue, "All", StringComparison.OrdinalIgnoreCase))
            {
                var status = ContractDebtStatusFilterValue switch
                {
                    "FullyPaid" => DebtStatus.FullyPaid,
                    "PartiallyPaid" => DebtStatus.PartiallyPaid,
                    "Overdue" => DebtStatus.Overdue,
                    _ => (DebtStatus?)null
                };

                if (status.HasValue && contract.DebtStatus != status.Value)
                    return false;
            }

            return true;
        }

        private bool PaymentFilterPredicate(object obj)
        {
            if (obj is not Payment payment)
                return false;

            var term = PaymentSearchText?.Trim() ?? string.Empty;
            if (term.Length == 0)
                return true;

            var tenant = payment.Contract?.Tenant?.Name ?? string.Empty;
            var prop = payment.Contract?.Property?.Name ?? string.Empty;
            var amountCur = payment.Amount.ToString(CultureInfo.CurrentCulture);
            var amountInv = payment.Amount.ToString(CultureInfo.InvariantCulture);
            var contractId = payment.ContractId.ToString(CultureInfo.InvariantCulture);

            return tenant.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prop.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   amountCur.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   amountInv.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   contractId.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EnsureLandlord()
        {
            if (App.Auth.IsLandlord)
                return true;

            MessageBox.Show("Это действие доступно только арендодателю.", "Доступ запрещён", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static bool EnsureTenantCanManagePayments()
        {
            if (!App.Auth.IsLandlord)
                return true;

            MessageBox.Show("Вносить и изменять платежи может только арендатор. У арендодателя доступен просмотр, поиск и экспорт.", "Платежи",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private bool CanAccessContract(Contract? contract)
        {
            if (contract == null)
                return false;
            if (IsLandlord)
                return true;
            return App.Auth.TenantId == contract.TenantId;
        }

        private bool CanAccessPayment(Payment? payment)
        {
            if (payment == null)
                return false;
            return CanAccessContract(payment.Contract);
        }

        private int GetUnpaidContractsCount()
        {
            return Contracts.Count(c => c.Debt > 0m);
        }

        private void UpdateUnpaidNotificationAndAnalyticsAndSchedule()
        {
            var unpaidCount = GetUnpaidContractsCount();

            UnpaidNotificationVisibility = unpaidCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnpaidNotificationText = unpaidCount > 0
                ? $"{unpaidCount} договор(ов) имеют задолженность (Задолженность > 0)."
                : "Все договоры полностью оплачены.";

            GenerateScheduleForSelectedContract();
            BuildAnalyticsModels();
        }

        private void StartNotificationTimer()
        {
            _notificationTimer.Interval = TimeSpan.FromMinutes(15);
            _notificationTimer.Tick -= NotificationTimer_Tick;
            _notificationTimer.Tick += NotificationTimer_Tick;
            _notificationTimer.Start();
        }

        private DateTime _lastPopupTime = DateTime.MinValue;
        private void NotificationTimer_Tick(object? sender, EventArgs e)
        {
            var unpaidCount = GetUnpaidContractsCount();
            var changed = unpaidCount != _lastUnpaidContractsCount;

            if (changed)
            {
                // Throttle popups to at most once per hour.
                if (unpaidCount > 0 && DateTime.Now - _lastPopupTime > TimeSpan.FromHours(1))
                {
                    _lastPopupTime = DateTime.Now;
                    MessageBox.Show($"{unpaidCount} договор(ов) сейчас имеют задолженность.", "Договоры с задолженностью", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                _lastUnpaidContractsCount = unpaidCount;
            }

            UpdateUnpaidNotificationAndAnalyticsAndSchedule();
        }

        private void GenerateScheduleForSelectedContract()
        {
            ContractScheduleItems.Clear();

            if (SelectedContract == null)
                return;

            var contract = SelectedContract;
            if (contract.EndDate.HasValue && contract.EndDate.Value.Date < contract.StartDate.Date)
                return;

            var start = contract.StartDate.Date;
            var end = contract.EndDate?.Date ?? DateTime.Today.Date;
            if (end < start)
                return;

            // Build expected monthly payment dates preserving the start day.
            var expectedDates = new List<DateTime>();
            var current = start;
            while (current.Date <= end && expectedDates.Count < 600)
            {
                expectedDates.Add(current.Date);
                current = current.AddMonths(1);
            }

            var paymentsOrdered = contract.Payments
                .OrderBy(p => p.PaymentDate)
                .ThenBy(p => p.Id)
                .ToList();

            int paymentIndex = 0;
            decimal paymentRemaining = paymentsOrdered.Count > 0 ? paymentsOrdered[0].Amount : 0m;

            foreach (var expectedDate in expectedDates)
            {
                decimal due = contract.MonthlyRent;
                decimal paid = 0m;

                while (due > 0m && paymentIndex < paymentsOrdered.Count)
                {
                    var take = Math.Min(due, paymentRemaining);
                    paid += take;
                    due -= take;
                    paymentRemaining -= take;

                    if (paymentRemaining <= 0m)
                    {
                        paymentIndex++;
                        paymentRemaining = paymentIndex < paymentsOrdered.Count ? paymentsOrdered[paymentIndex].Amount : 0m;
                    }
                }

                ContractScheduleItems.Add(new ContractScheduleItem
                {
                    ExpectedDate = expectedDate,
                    ExpectedAmount = contract.MonthlyRent,
                    PaidAmount = paid
                });
            }
        }

        private void BuildAnalyticsModels()
        {
            // Contracts per property.
            var contractsPerProperty = Contracts
                .GroupBy(c => c.Property?.Name ?? $"Объект #{c.PropertyId}")
                .OrderBy(g => g.Key)
                .ToList();

            var model1 = new PlotModel { Title = "Договоров по объектам" };
            model1.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                IsAxisVisible = true,
                ItemsSource = contractsPerProperty.Select(g => g.Key).ToArray()
            });
            model1.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Количество договоров",
                IsAxisVisible = true
            });

            var barSeries1 = new BarSeries();
            foreach (var g in contractsPerProperty)
                barSeries1.Items.Add(new BarItem { Value = g.Count() });
            model1.Series.Add(barSeries1);

            // Debt per tenant.
            var debtPerTenant = Contracts
                .GroupBy(c => c.Tenant?.Name ?? $"Арендатор #{c.TenantId}")
                .OrderBy(g => g.Key)
                .ToList();

            var model2 = new PlotModel { Title = "Общая задолженность по арендаторам" };
            model2.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Left,
                IsAxisVisible = true,
                ItemsSource = debtPerTenant.Select(g => g.Key).ToArray()
            });
            model2.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Задолженность",
                IsAxisVisible = true
            });

            var barSeries2 = new BarSeries();
            foreach (var g in debtPerTenant)
                barSeries2.Items.Add(new BarItem { Value = (double)g.Sum(c => c.Debt) });
            model2.Series.Add(barSeries2);

            // Payment history over time.
            var paymentsHistory = Payments
                .GroupBy(p => new DateTime(p.PaymentDate.Year, p.PaymentDate.Month, 1))
                .OrderBy(g => g.Key)
                .ToList();

            var model3 = new PlotModel { Title = "История платежей по времени" };
            model3.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "yyyy-MM",
                IntervalType = DateTimeIntervalType.Months,
                IsAxisVisible = true
            });
            model3.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Оплачено",
                IsAxisVisible = true
            });

            var line = new LineSeries { MarkerType = MarkerType.Circle };
            foreach (var item in paymentsHistory)
            {
                line.Points.Add(new DataPoint(DateTimeAxis.ToDouble(item.Key), (double)item.Sum(p => p.Amount)));
            }
            model3.Series.Add(line);

            ContractsPerPropertyPlotView.Model = model1;
            DebtPerTenantPlotView.Model = model2;
            PaymentHistoryPlotView.Model = model3;
        }

        private void ReloadData_Click(object sender, RoutedEventArgs e)
        {
            var selectedPropertyId = SelectedProperty?.Id;
            var selectedTenantId = SelectedTenant?.Id;
            var selectedContractId = SelectedContract?.Id;
            var selectedPaymentId = SelectedPayment?.Id;

            LoadData();

            if (selectedPropertyId.HasValue)
                SelectedProperty = Properties.FirstOrDefault(p => p.Id == selectedPropertyId.Value);
            if (selectedTenantId.HasValue)
                SelectedTenant = Tenants.FirstOrDefault(t => t.Id == selectedTenantId.Value);
            if (selectedContractId.HasValue)
                SelectedContract = Contracts.FirstOrDefault(c => c.Id == selectedContractId.Value);
            if (selectedPaymentId.HasValue)
                SelectedPayment = Payments.FirstOrDefault(p => p.Id == selectedPaymentId.Value);
        }

        private void DeleteSelectedContracts_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            var selectedIds = Contracts.Where(c => c.IsSelectedForBulkDelete).Select(c => c.Id).ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Не выбраны договоры для удаления.");
                return;
            }

            var confirm = MessageBox.Show($"Удалить {selectedIds.Count} договор(ов) и связанные платежи?", "Подтверждение удаления",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            var toDelete = _context.Contracts.Where(c => selectedIds.Contains(c.Id)).ToList();
            foreach (var c in toDelete)
            {
                if (!CanAccessContract(c))
                {
                    MessageBox.Show("Нельзя удалить один из выбранных договоров: нет доступа.", "Доступ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _context.Contracts.RemoveRange(toDelete);
            _context.SaveChanges();

            RefreshContractsAndPayments();
        }

        private void ClearSelectedContracts_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            foreach (var c in Contracts)
                c.IsSelectedForBulkDelete = false;

            _contractsView.Refresh();
        }

        private void DeleteSelectedPayments_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTenantCanManagePayments())
                return;

            var selectedIds = Payments.Where(p => p.IsSelectedForBulkDelete).Select(p => p.Id).ToList();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Не выбраны платежи для удаления.");
                return;
            }

            var confirm = MessageBox.Show($"Удалить {selectedIds.Count} платеж(ей)?", "Подтверждение удаления",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            var toDelete = _context.Payments
                .Include(p => p.Contract)
                .Where(p => selectedIds.Contains(p.Id))
                .ToList();

            foreach (var p in toDelete)
            {
                if (!CanAccessPayment(p))
                {
                    MessageBox.Show("Нельзя удалить один из выбранных платежей: нет доступа.", "Доступ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _context.Payments.RemoveRange(toDelete);
            _context.SaveChanges();

            RefreshContractsAndPayments();
        }

        private void ClearSelectedPayments_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTenantCanManagePayments())
                return;

            foreach (var p in Payments)
                p.IsSelectedForBulkDelete = false;
            PaymentsDataGrid.Items.Refresh();
            _paymentsView.Refresh();
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }

        private void ExportContractsToExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                FileName = $"Договоры_{DateTime.Today:yyyy-MM-dd}.xlsx"
            };

            if (dlg.ShowDialog() != true)
                return;

            var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Договоры");

            ws.Cell(1, 1).Value = "ID";
            ws.Cell(1, 2).Value = "Арендатор";
            ws.Cell(1, 3).Value = "Объект";
            ws.Cell(1, 4).Value = "Дата начала";
            ws.Cell(1, 5).Value = "Дата окончания";
            ws.Cell(1, 6).Value = "Аренда в месяц";
            ws.Cell(1, 7).Value = "Задолженность";
            ws.Cell(1, 8).Value = "Статус";

            int row = 2;
            foreach (var c in ContractsView.Cast<Contract>())
            {
                ws.Cell(row, 1).Value = c.Id;
                ws.Cell(row, 2).Value = c.Tenant?.Name ?? $"Арендатор #{c.TenantId}";
                ws.Cell(row, 3).Value = c.Property?.Name ?? $"Объект #{c.PropertyId}";
                ws.Cell(row, 4).Value = c.StartDate.Date;
                ws.Cell(row, 5).Value = c.EndDate?.Date;
                ws.Cell(row, 6).Value = c.MonthlyRent;
                ws.Cell(row, 7).Value = c.Debt;
                ws.Cell(row, 8).Value = c.DebtStatusText;
                row++;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dlg.FileName);
            MessageBox.Show("Договоры экспортированы в Excel.");
        }

        private void ExportContractsToPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Документ PDF (*.pdf)|*.pdf",
                FileName = $"Договоры_{DateTime.Today:yyyy-MM-dd}.pdf"
            };

            if (dlg.ShowDialog() != true)
                return;

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10, XFontStyle.Regular);
            var bold = new XFont("Verdana", 10, XFontStyle.Bold);

            double margin = 40;
            double y = margin;
            gfx.DrawString("Отчет по договорам", bold, XBrushes.Black, new XPoint(margin, y));
            y += 20;

            string header = "ID | Арендатор | Объект | Дата начала | Дата окончания | Аренда в месяц | Задолженность | Статус";
            gfx.DrawString(header, font, XBrushes.Black, new XPoint(margin, y));
            y += 14;

            var lineHeight = 14;
            foreach (var c in ContractsView.Cast<Contract>())
            {
                if (y > page.Height - margin - lineHeight)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }

                var tenant = Truncate(c.Tenant?.Name ?? $"Арендатор #{c.TenantId}", 20);
                var property = Truncate(c.Property?.Name ?? $"Объект #{c.PropertyId}", 25);
                var end = c.EndDate.HasValue ? c.EndDate.Value.ToString("yyyy-MM-dd") : "-";

                string line = $"{c.Id} | {tenant} | {property} | {c.StartDate:yyyy-MM-dd} | {end} | {c.MonthlyRent} | {c.Debt} | {c.DebtStatusText}";
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
                y += lineHeight;
            }

            document.Save(dlg.FileName);
            MessageBox.Show("Договоры экспортированы в PDF.");
        }

        private void ExportPaymentsToExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                FileName = $"Платежи_{DateTime.Today:yyyy-MM-dd}.xlsx"
            };

            if (dlg.ShowDialog() != true)
                return;

            var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Платежи");

            ws.Cell(1, 1).Value = "№";
            ws.Cell(1, 2).Value = "№ договора";
            ws.Cell(1, 3).Value = "Арендатор";
            ws.Cell(1, 4).Value = "Объект";
            ws.Cell(1, 5).Value = "Дата платежа";
            ws.Cell(1, 6).Value = "Сумма";
            ws.Cell(1, 7).Value = "Задолженность по договору";
            ws.Cell(1, 8).Value = "Просрочено";

            int row = 2;
            foreach (var p in PaymentsView.Cast<Payment>().OrderBy(p => p.PaymentDate))
            {
                ws.Cell(row, 1).Value = p.Id;
                ws.Cell(row, 2).Value = p.ContractId;
                ws.Cell(row, 3).Value = p.Contract?.Tenant?.Name ?? $"Арендатор #{p.Contract?.TenantId ?? 0}";
                ws.Cell(row, 4).Value = p.Contract?.Property?.Name ?? $"Объект #{p.Contract?.PropertyId ?? 0}";
                ws.Cell(row, 5).Value = p.PaymentDate.Date;
                ws.Cell(row, 6).Value = p.Amount;
                ws.Cell(row, 7).Value = p.Contract?.Debt ?? 0m;
                ws.Cell(row, 8).Value = p.IsOverdueText;
                row++;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(dlg.FileName);
            MessageBox.Show("Платежи экспортированы в Excel.");
        }

        private void ExportPaymentsToPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Документ PDF (*.pdf)|*.pdf",
                FileName = $"Платежи_{DateTime.Today:yyyy-MM-dd}.pdf"
            };

            if (dlg.ShowDialog() != true)
                return;

            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10, XFontStyle.Regular);
            var bold = new XFont("Verdana", 10, XFontStyle.Bold);

            double margin = 40;
            double y = margin;
            gfx.DrawString("Отчет по платежам", bold, XBrushes.Black, new XPoint(margin, y));
            y += 20;

            string header = "ID | ID договора | Арендатор | Объект | Дата платежа | Сумма | Задолженность по договору | Просрочено";
            gfx.DrawString(header, font, XBrushes.Black, new XPoint(margin, y));
            y += 14;

            var lineHeight = 14;
            foreach (var p in PaymentsView.Cast<Payment>().OrderBy(p => p.PaymentDate))
            {
                if (y > page.Height - margin - lineHeight)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }

                var tenant = Truncate(p.Contract?.Tenant?.Name ?? "?", 20);
                var property = Truncate(p.Contract?.Property?.Name ?? "?", 25);
                var end = p.PaymentDate.ToString("yyyy-MM-dd");
                var debt = p.Contract?.Debt ?? 0m;
                var overdue = p.IsOverdue ? "Да" : "Нет";

                string line = $"{p.Id} | {p.ContractId} | {tenant} | {property} | {end} | {p.Amount} | {debt} | {overdue}";
                gfx.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
                y += lineHeight;
            }

            document.Save(dlg.FileName);
            MessageBox.Show("Платежи экспортированы в PDF.");
        }

        private void AddProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (string.IsNullOrWhiteSpace(PropertyNameInput))
            {
                MessageBox.Show("Введите название объекта недвижимости.");
                return;
            }

            if (!decimal.TryParse(PropertyPriceInput, out var price) || price < 0)
            {
                MessageBox.Show("Некорректная арендная плата в месяц.");
                return;
            }

            var property = new Property
            {
                Name = PropertyNameInput.Trim(),
                PricePerMonth = price
            };

            _context.Properties.Add(property);
            _context.SaveChanges();

            Properties.Add(property);
            PropertyNameInput = string.Empty;
            PropertyPriceInput = string.Empty;
        }

        private void EditProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedProperty == null)
            {
                MessageBox.Show("Выберите объект для редактирования.");
                return;
            }

            if (string.IsNullOrWhiteSpace(PropertyNameInput))
            {
                MessageBox.Show("Введите название объекта недвижимости.");
                return;
            }

            if (!decimal.TryParse(PropertyPriceInput, out var price) || price < 0)
            {
                MessageBox.Show("Некорректная арендная плата в месяц.");
                return;
            }

            var property = _context.Properties.Find(SelectedProperty.Id);
            if (property == null) return;

            property.Name = PropertyNameInput.Trim();
            property.PricePerMonth = price;
            _context.SaveChanges();

            SelectedProperty.Name = property.Name;
            SelectedProperty.PricePerMonth = property.PricePerMonth;

            var index = Properties.IndexOf(SelectedProperty);
            if (index >= 0)
            {
                Properties.RemoveAt(index);
                Properties.Insert(index, property);
                SelectedProperty = property;
            }

            RefreshContractsAndPayments();

            if (SelectedPropertyForContract?.Id == property.Id)
                SelectedPropertyForContract = property;
        }

        private void DeleteProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedProperty == null)
            {
                MessageBox.Show("Выберите объект для удаления.");
                return;
            }

            var hasContracts = _context.Contracts.Any(c => c.PropertyId == SelectedProperty.Id);
            if (hasContracts)
            {
                MessageBox.Show("Нельзя удалить объект с существующими договорами.");
                return;
            }

            var property = _context.Properties.Find(SelectedProperty.Id);
            if (property == null) return;

            _context.Properties.Remove(property);
            _context.SaveChanges();

            Properties.Remove(SelectedProperty);
            SelectedProperty = null;
        }

        private void AddTenant_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (string.IsNullOrWhiteSpace(TenantNameInput))
            {
                MessageBox.Show("Введите имя арендатора.");
                return;
            }

            var tenant = new Tenant
            {
                Name = TenantNameInput.Trim()
            };

            _context.Tenants.Add(tenant);
            _context.SaveChanges();

            Tenants.Add(tenant);
            TenantNameInput = string.Empty;
        }

        private void EditTenant_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedTenant == null)
            {
                MessageBox.Show("Выберите арендатора для редактирования.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TenantNameInput))
            {
                MessageBox.Show("Введите имя арендатора.");
                return;
            }

            var tenant = _context.Tenants.Find(SelectedTenant.Id);
            if (tenant == null) return;

            tenant.Name = TenantNameInput.Trim();
            _context.SaveChanges();

            SelectedTenant.Name = tenant.Name;

            var index = Tenants.IndexOf(SelectedTenant);
            if (index >= 0)
            {
                Tenants.RemoveAt(index);
                Tenants.Insert(index, tenant);
                SelectedTenant = tenant;
            }

            RefreshContractsAndPayments();
        }

        private void DeleteTenant_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedTenant == null)
            {
                MessageBox.Show("Выберите арендатора для удаления.");
                return;
            }

            var hasContracts = _context.Contracts.Any(c => c.TenantId == SelectedTenant.Id);
            if (hasContracts)
            {
                MessageBox.Show("Нельзя удалить арендатора с существующими договорами.");
                return;
            }

            var linkedUsers = _context.Users.Where(u => u.TenantId == SelectedTenant.Id).ToList();
            if (linkedUsers.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "У этого арендатора есть учётная запись для входа в программу. При удалении карточки она тоже будет удалена. Продолжить?",
                    "Удаление арендатора",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            var tenant = _context.Tenants.Find(SelectedTenant.Id);
            if (tenant == null) return;

            foreach (var user in linkedUsers)
                _context.Users.Remove(user);

            _context.Tenants.Remove(tenant);

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось удалить арендатора: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var removedId = SelectedTenant.Id;
            Tenants.Remove(SelectedTenant);
            SelectedTenant = null;
            if (SelectedTenantForContract?.Id == removedId)
                SelectedTenantForContract = null;
        }

        private void AddContract_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedTenantForContract == null)
            {
                MessageBox.Show("Выберите арендатора.");
                return;
            }

            if (SelectedPropertyForContract == null)
            {
                MessageBox.Show("Выберите объект.");
                return;
            }

            if (!ContractStartDateInput.HasValue)
            {
                MessageBox.Show("Выберите дату начала.");
                return;
            }

            if (ContractEndDateInput.HasValue && ContractEndDateInput.Value.Date < ContractStartDateInput.Value.Date)
            {
                MessageBox.Show("Дата окончания должна быть не меньше даты начала.");
                return;
            }

            var monthlyRent = SelectedPropertyForContract.PricePerMonth;
            if (monthlyRent <= 0)
            {
                MessageBox.Show("У выбранного объекта не задана корректная арендная плата в месяц.");
                return;
            }

            var contract = new Contract
            {
                TenantId = SelectedTenantForContract.Id,
                PropertyId = SelectedPropertyForContract.Id,
                StartDate = ContractStartDateInput.Value.Date,
                EndDate = ContractEndDateInput?.Date,
                MonthlyRent = monthlyRent
            };

            _context.Contracts.Add(contract);
            _context.SaveChanges();

            _context.Entry(contract).Reference(c => c.Tenant).Load();
            _context.Entry(contract).Reference(c => c.Property).Load();
            _context.Entry(contract).Collection(c => c.Payments).Load();

            Contracts.Add(contract);

            ContractStartDateInput = DateTime.Today;
            ContractEndDateInput = null;
            UpdateContractMonthlyRentDisplay();
        }

        private void EditContract_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedContract == null)
            {
                MessageBox.Show("Выберите договор для редактирования.");
                return;
            }

            if (SelectedTenantForContract == null)
            {
                MessageBox.Show("Выберите арендатора.");
                return;
            }

            if (SelectedPropertyForContract == null)
            {
                MessageBox.Show("Выберите объект.");
                return;
            }

            if (!ContractStartDateInput.HasValue)
            {
                MessageBox.Show("Выберите дату начала.");
                return;
            }

            if (ContractEndDateInput.HasValue && ContractEndDateInput.Value.Date < ContractStartDateInput.Value.Date)
            {
                MessageBox.Show("Дата окончания должна быть не меньше даты начала.");
                return;
            }

            var monthlyRent = SelectedPropertyForContract.PricePerMonth;
            if (monthlyRent <= 0)
            {
                MessageBox.Show("У выбранного объекта не задана корректная арендная плата в месяц.");
                return;
            }

            var contract = _context.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Property)
                .Include(c => c.Payments)
                .FirstOrDefault(c => c.Id == SelectedContract.Id);

            if (contract == null) return;
            if (!CanAccessContract(contract))
                return;

            contract.TenantId = SelectedTenantForContract.Id;
            contract.PropertyId = SelectedPropertyForContract.Id;
            contract.StartDate = ContractStartDateInput.Value.Date;
            contract.EndDate = ContractEndDateInput?.Date;
            contract.MonthlyRent = monthlyRent;

            _context.SaveChanges();

            RefreshContractsAndPayments();
        }

        private void DeleteContract_Click(object sender, RoutedEventArgs e)
        {
            // Deletion is checkbox-driven: remove only contracts marked in the left "Удалить" column.
            DeleteSelectedContracts_Click(sender, e);
        }

        private static string GetContractFilesStorageDirectory()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RealEstateRental",
                "ContractFiles");
            Directory.CreateDirectory(baseDir);
            return baseDir;
        }

        private static bool IsSupportedContractFile(string path)
        {
            var ext = Path.GetExtension(path);
            return AllowedContractFileExtensions.Contains(ext);
        }

        private void AttachContractFile_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedContract == null)
            {
                MessageBox.Show("Сначала выберите договор в таблице.");
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Документы договора (*.pdf;*.docx)|*.pdf;*.docx",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != true)
                return;

            if (!IsSupportedContractFile(dlg.FileName))
            {
                MessageBox.Show("Поддерживаются только файлы PDF и DOCX.");
                return;
            }

            var contract = _context.Contracts.FirstOrDefault(c => c.Id == SelectedContract.Id);
            if (contract == null) return;
            if (!CanAccessContract(contract))
                return;

            var storageDir = GetContractFilesStorageDirectory();
            var fileName = $"{contract.Id}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(dlg.FileName)}";
            var destinationPath = Path.Combine(storageDir, fileName);

            File.Copy(dlg.FileName, destinationPath, overwrite: true);

            if (!string.IsNullOrWhiteSpace(contract.DocumentFilePath) && File.Exists(contract.DocumentFilePath))
            {
                try { File.Delete(contract.DocumentFilePath); } catch { /* Ignore stale file delete errors. */ }
            }

            contract.DocumentFilePath = destinationPath;
            _context.SaveChanges();
            RefreshContractsAndPayments();
        }

        private void OpenContractFile_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedContract == null)
            {
                MessageBox.Show("Сначала выберите договор в таблице.");
                return;
            }

            if (!CanAccessContract(SelectedContract))
                return;

            var path = SelectedContract.DocumentFilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("У выбранного договора нет прикреплённого файла.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private void SaveContractFileAs_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedContract == null)
            {
                MessageBox.Show("Сначала выберите договор в таблице.");
                return;
            }

            if (!CanAccessContract(SelectedContract))
                return;

            var path = SelectedContract.DocumentFilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("У выбранного договора нет прикреплённого файла.");
                return;
            }

            var ext = Path.GetExtension(path);
            var filter = ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? "Документ Word (*.docx)|*.docx"
                : "Документ PDF (*.pdf)|*.pdf";

            var dlg = new SaveFileDialog
            {
                Filter = filter,
                FileName = $"Договор_{SelectedContract.Id}{ext}"
            };

            if (dlg.ShowDialog() != true)
                return;

            File.Copy(path, dlg.FileName, overwrite: true);
            MessageBox.Show("Файл договора сохранён.");
        }

        private void RemoveContractFile_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureLandlord())
                return;

            if (SelectedContract == null)
            {
                MessageBox.Show("Сначала выберите договор в таблице.");
                return;
            }

            var contract = _context.Contracts.FirstOrDefault(c => c.Id == SelectedContract.Id);
            if (contract == null) return;
            if (!CanAccessContract(contract))
                return;

            if (string.IsNullOrWhiteSpace(contract.DocumentFilePath))
            {
                MessageBox.Show("У выбранного договора нет прикреплённого файла.");
                return;
            }

            var confirm = MessageBox.Show(
                "Удалить прикреплённый файл у выбранного договора?",
                "Удаление файла договора",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            var oldPath = contract.DocumentFilePath;
            contract.DocumentFilePath = null;
            _context.SaveChanges();

            if (!string.IsNullOrWhiteSpace(oldPath) && File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { /* Ignore physical file delete errors. */ }
            }

            RefreshContractsAndPayments();
        }

        private void AddPayment_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTenantCanManagePayments())
                return;

            if (SelectedContractForPayment == null)
            {
                MessageBox.Show("Выберите договор.");
                return;
            }

            if (!PaymentDateInput.HasValue)
            {
                MessageBox.Show("Выберите дату платежа.");
                return;
            }

            var contract = SelectedContractForPayment;
            if (!CanAccessContract(contract))
                return;

            var paymentDate = PaymentDateInput.Value.Date;
            if (paymentDate < contract.StartDate.Date)
            {
                MessageBox.Show("Дата платежа не может быть раньше даты начала договора.");
                return;
            }

            if (contract.EndDate.HasValue && paymentDate > contract.EndDate.Value.Date)
            {
                MessageBox.Show("Дата платежа не может быть позже даты окончания договора.");
                return;
            }

            if (!decimal.TryParse(PaymentAmountInput, out var amount) || amount <= 0)
            {
                MessageBox.Show("Некорректная сумма платежа.");
                return;
            }

            var payment = new Payment
            {
                ContractId = SelectedContractForPayment.Id,
                PaymentDate = paymentDate,
                Amount = amount
            };

            _context.Payments.Add(payment);
            _context.SaveChanges();

            _context.Entry(payment).Reference(p => p.Contract)!.Load();
            _context.Entry(payment.Contract!).Reference(c => c.Tenant).Load();
            _context.Entry(payment.Contract!).Reference(c => c.Property).Load();
            _context.Entry(payment.Contract!).Collection(c => c.Payments).Load();

            Payments.Add(payment);
            RefreshContractsAndPayments();

            PaymentDateInput = DateTime.Today;
            PaymentAmountInput = string.Empty;
        }

        private void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTenantCanManagePayments())
                return;

            if (SelectedPayment == null)
            {
                MessageBox.Show("Выберите платеж для редактирования.");
                return;
            }

            if (SelectedContractForPayment == null)
            {
                MessageBox.Show("Выберите договор.");
                return;
            }

            if (!PaymentDateInput.HasValue)
            {
                MessageBox.Show("Выберите дату платежа.");
                return;
            }

            var contract = SelectedContractForPayment;
            if (!CanAccessContract(contract))
                return;

            var paymentDate = PaymentDateInput.Value.Date;
            if (paymentDate < contract.StartDate.Date)
            {
                MessageBox.Show("Дата платежа не может быть раньше даты начала договора.");
                return;
            }

            if (contract.EndDate.HasValue && paymentDate > contract.EndDate.Value.Date)
            {
                MessageBox.Show("Дата платежа не может быть позже даты окончания договора.");
                return;
            }

            if (!decimal.TryParse(PaymentAmountInput, out var amount) || amount <= 0)
            {
                MessageBox.Show("Некорректная сумма платежа.");
                return;
            }

            var payment = _context.Payments
                .Include(p => p.Contract)!.ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Property)
                .Include(p => p.Contract)!.ThenInclude(c => c!.Payments)
                .FirstOrDefault(p => p.Id == SelectedPayment.Id);

            if (payment == null) return;
            if (!CanAccessPayment(payment))
                return;

            payment.ContractId = SelectedContractForPayment.Id;
            payment.PaymentDate = paymentDate;
            payment.Amount = amount;

            _context.SaveChanges();

            RefreshContractsAndPayments();
        }

        private void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTenantCanManagePayments())
                return;

            if (SelectedPayment == null)
            {
                MessageBox.Show("Выберите платеж для удаления.");
                return;
            }

            var payment = _context.Payments
                .Include(p => p.Contract)
                .FirstOrDefault(p => p.Id == SelectedPayment.Id);
            if (payment == null) return;
            if (!CanAccessPayment(payment))
                return;

            _context.Payments.Remove(payment);
            _context.SaveChanges();

            Payments.Remove(SelectedPayment);
            SelectedPayment = null;

            RefreshContractsAndPayments();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из учётной записи?", "Выход", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            App.Auth.Clear();
            _closingForRelogin = true;
            Close();
            _closingForRelogin = false;

            var login = new LoginWindow();
            if (login.ShowDialog() == true)
            {
                new MainWindow().Show();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _context.Dispose();
            if (!_closingForRelogin)
                Application.Current.Shutdown();
        }
    }
}