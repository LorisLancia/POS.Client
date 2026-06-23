using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using POS.Client.Data;
using POS.Client.Models;
using POS.Client.Services;
using SyncService = POS.Client.Services.SyncService;

namespace POS.Client.Views
{
    public partial class CashierWindow : Window
    {
        private readonly SyncService _syncService;
        private readonly ApiService _apiService;
        private List<CartItem> _cart = new();
        private decimal _subtotal = 0;
        private decimal _tax = 0;
        private decimal _total = 0;
        private LocalProduct _selectedProduct = null;

        public CashierWindow()
        {
            InitializeComponent();
            _syncService = new SyncService();
            _apiService = new ApiService();
            _apiService.SetToken(AppState.AuthToken);
            LoadProducts();
            OpenShiftIfNeeded();
        }

        private void OpenShiftIfNeeded()
        {
            var db = new POSDbContext();

            // Cerca SEMPRE uno shift aperto in SQLite (persiste anche se chiudi app)
            var localOpen = db.Shifts.FirstOrDefault(s => s.Status == "open");
            if (localOpen != null)
            {
                AppState.CurrentShiftId = localOpen.LocalId;
                string serverTag = localOpen.ServerId.HasValue ? $"Server#{localOpen.ServerId}" : "Local";
                Title = $"Cashier - Shift #{localOpen.LocalId} ({serverTag})";
                return;
            }

            // Nessuno shift aperto: crea nuovo
            var localShift = new LocalShift
            {
                PosClientId = 1, // TODO: da config hardware
                UserId = AppState.CurrentUserId > 0 ? AppState.CurrentUserId : 1,
                StartingCash = 1000, // TODO: da config o input iniziale
                Status = "open",
                OpenedAt = DateTime.Now
            };
            db.Shifts.Add(localShift);
            db.SaveChanges();

            AppState.CurrentShiftId = localShift.LocalId;
            Title = $"Cashier - Shift #{localShift.LocalId} (Local)";
        }

        private void LoadProducts()
        {
            var products = _syncService.GetProducts();
            icProducts.ItemsSource = products;
        }

        private void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var productId = (int)button.Tag;

            var product = _syncService.GetProducts().FirstOrDefault(p => p.ServerId == productId);
            if (product == null) return;

            _selectedProduct = product;

            var variants = _syncService.GetVariants(product.ServerId);
            if (variants.Count > 0)
            {
                cbVariants.ItemsSource = variants;
                cbVariants.Tag = product;
                return;
            }

            AddToCart(product.Name, product.BasePrice, product.TaxRate);
        }

        private void cbVariants_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var variant = cbVariants.SelectedItem as LocalProductVariant;
            if (variant == null || _selectedProduct == null) return;

            var price = _selectedProduct.BasePrice + variant.PriceAdjustment;
            AddToCart($"{_selectedProduct.Name} ({variant.Name})", price, _selectedProduct.TaxRate);
            cbVariants.SelectedItem = null;
        }

        private void AddToCart(string name, decimal price, decimal taxRate)
        {
            _cart.Add(new CartItem { Name = name, Price = price, TaxRate = taxRate });
            UpdateCartDisplay();
        }

        private void UpdateCartDisplay()
        {
            lbCart.Items.Clear();
            _subtotal = 0;
            _tax = 0;

            foreach (var item in _cart)
            {
                lbCart.Items.Add($"{item.Name} - {item.Price:F2} THB");
                _subtotal += item.Price;
                _tax += item.Price * (item.TaxRate / 100);
            }

            _total = _subtotal + _tax;

            txtSubtotal.Text = $"Subtotal: {_subtotal:F2}";
            txtTax.Text = $"Tax: {_tax:F2}";
            txtTotal.Text = $"Total: {_total:F2}";
        }

        private async void btnPay_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Cart is empty!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AppState.CurrentShiftId == 0)
            {
                MessageBox.Show("No open shift! Please restart Cashier.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var token = AppState.AuthToken;
                if (string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Please login first!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var sale = new LocalSale
                {
                    StoreId = AppState.CurrentStoreId,
                    WarehouseId = 1,
                    PosClientId = 1,
                    ShiftId = AppState.CurrentShiftId,
                    UserId = AppState.CurrentUserId > 0 ? AppState.CurrentUserId : 1,
                    Subtotal = _subtotal,
                    TaxTotal = _tax,
                    DiscountTotal = 0,
                    Total = _total,
                    CreatedAt = DateTime.Now
                };

                var items = new List<LocalSaleItem>();
                var payments = new List<LocalPayment>();

                foreach (var cartItem in _cart)
                {
                    var product = _syncService.GetProducts().FirstOrDefault(p => cartItem.Name.StartsWith(p.Name));
                    if (product == null) continue;

                    items.Add(new LocalSaleItem
                    {
                        ProductId = product.ServerId,
                        Quantity = 1,
                        UnitPrice = cartItem.Price,
                        TotalPrice = cartItem.Price,
                        DiscountAmount = 0
                    });
                }

                payments.Add(new LocalPayment
                {
                    Method = "cash",
                    Amount = _total,
                    Reference = ""
                });

                var queue = new OfflineQueueService(token);
                int localId = queue.QueueSale(sale, items, payments);

                int pending = queue.GetPendingCount();

                MessageBox.Show(
                    $"✅ SALE SAVED\n\n" +
                    $"Local ID: {localId}\n" +
                    $"Total: {_total:F2} THB\n" +
                    $"Pending sync: {pending} sale(s)\n\n" +
                    $"Will sync automatically when server is available.",
                    "Sale Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                _cart.Clear();
                UpdateCartDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class CartItem
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public decimal TaxRate { get; set; }
    }
}