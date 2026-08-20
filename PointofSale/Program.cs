using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;       // <-- NuGet package for JSON file handling
using System.IO;             // for File operations

namespace PointofSale
{
    // ===== BASE CLASS FOR INHERITANCE =====
    class Entity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // ===== PRODUCT INHERITS FROM ENTITY =====
    class Product : Entity
    {
        public double Price { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
    }

    // ===== USER CLASS =====
    class User
    {
        private string userName;
        private int password;

        public string UserName
        {
            get { return userName; }
            set { userName = value; }
        }
        public int Password
        {
            get { return password; }
            set { password = value; }
        }
    }

    // ===== CART ITEM (for line items) =====
    class CartItem
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public double LineTotal => Product.Price * Quantity;
    }

    // ===== PRODUCT MANAGER (with JSON save/load) =====
    class ProductManager
    {
        public Dictionary<int, Product> productCatalog = new Dictionary<int, Product>();

        // ----- Save catalog to JSON file using Newtonsoft.Json -----
        public void SaveCatalog()
        {
            string json = JsonConvert.SerializeObject(productCatalog, Formatting.Indented);
            File.WriteAllText("products.json", json);
            // Console.WriteLine("Catalog saved to products.json");
        }

        // ----- Load catalog from JSON file -----
        public void LoadCatalog()
        {
            if (File.Exists("products.json"))
            {
                string json = File.ReadAllText("products.json");
                var loaded = JsonConvert.DeserializeObject<Dictionary<int, Product>>(json);
                if (loaded != null)
                {
                    productCatalog = loaded;
                    // Console.WriteLine($"Loaded {productCatalog.Count} products from file.");
                }
            }
        }

        // ----- Add Products -----
        public void AddProducts()
        {
            Console.WriteLine("Welcome to Product Inventory");
            while (true)
            {
                Console.Write("Please enter the Product ID: (or type 'x' to finish / 'm' to go to main menu): ");
                string idInput = Console.ReadLine();
                if (idInput.ToLower() == "x")
                {
                    Environment.Exit(0);
                }
                if (idInput.ToLower() == "m")
                {
                    Console.Clear();
                    Program.MainMenu();
                }
                if (!int.TryParse(idInput, out int productId) || productId == 0)
                {
                    Console.WriteLine("Invalid input! Please enter a valid product ID: ");
                    continue;
                }
                if (productCatalog.ContainsKey(productId))
                {
                    Console.WriteLine($"A product with ID {productId} already exists in the inventory!");
                    continue;
                }
                Console.WriteLine("Please enter the Product Name: ");
                string productName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(productName))
                {
                    Console.WriteLine("Product name cannot be empty!");
                    continue;
                }
                Console.WriteLine("Please enter the Unit Price: ");
                string pPrice = Console.ReadLine();
                if (!double.TryParse(pPrice, out double productPrice) || productPrice == 0)
                {
                    Console.WriteLine("Invalid input! Please enter a valid product price.");
                    continue;
                }
                Console.WriteLine("Please enter the Category: ");
                string productCategory = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(productCategory))
                {
                    Console.WriteLine("Product category cannot be empty!");
                    continue;
                }
                Console.WriteLine("Please enter the Quantity: ");
                string pQuantity = Console.ReadLine();
                if (!int.TryParse(pQuantity, out int productQuantity) || productQuantity == 0)
                {
                    Console.WriteLine("Invalid input! Please enter a valid product quantity.");
                    continue;
                }
                Product newProduct = new Product
                {
                    Id = productId,
                    Name = productName,
                    Price = productPrice,
                    Category = productCategory,
                    Quantity = productQuantity
                };
                productCatalog.Add(productId, newProduct);

                Console.WriteLine($"Successfully added | SKU: {productId} Name: {productName} Price: {productPrice} Category: {productCategory} Quantity: {productQuantity}");
                Console.WriteLine($"Dictionary Count = {productCatalog.Count}");

                SaveCatalog();  // auto-save after each addition

                Console.WriteLine("Press 0 to finish or 1 to go to main menu or any other key to add more products: ");
                string selection = Console.ReadLine();
                if (selection == "0")
                {
                    Environment.Exit(0);
                }
                if (selection == "1")
                {
                    Console.Clear();
                    return;
                }
                else
                {
                    continue;
                }
            }
        }

        // ----- View Products -----
        public void ViewProducts()
        {
            Console.WriteLine("Right Now! We have the following products:");
            if (productCatalog.Count == 0)
            {
                Console.WriteLine("No products were added!");
            }
            else
            {
                foreach (KeyValuePair<int, Product> item in productCatalog)
                {
                    Console.WriteLine($"SKU: {item.Key} | " + $"Name: {item.Value.Name} | " + $"Price: {item.Value.Price} | " + $"Category: {item.Value.Category} | " + $"Quantity: {item.Value.Quantity}");
                }
            }

            while (true)
            {
                Console.WriteLine("Press 0 to finish or 1 to go to main menu: ");
                string select = Console.ReadLine();
                if (select == "0")
                {
                        Environment.Exit(0);
                }
                if (select == "1")
                {
                    Console.Clear();
                    return;
                }
            }
        }

        // ----- Reduce stock (using 'ref') -----
        public bool ReduceStock(int sku, int quantity, ref int currentStock)
        {
            if (productCatalog.TryGetValue(sku, out Product product))
            {
                if (product.Quantity >= quantity)
                {
                    currentStock = product.Quantity - quantity;
                    product.Quantity = currentStock;
                    SaveCatalog();
                    return true;
                }
            }
            return false;
        }
    }

    // ===== SALE CLASS (handles cart, undo, totals, checkout) =====
    class Sale
    {
        private ProductManager products;
        private List<CartItem> cart;
        private Stack<Action> undoStack;
        private Queue<string> receiptQueue;

        // Arrays for tax
        private double[] taxThresholds = { 0, 100, 200, 500 };
        private double[] taxRates = { 0.05, 0.08, 0.10, 0.12 };

        public Sale(ProductManager products)
        {
            this.products = products;
            cart = new List<CartItem>();
            undoStack = new Stack<Action>();
            receiptQueue = new Queue<string>();
        }

        // ----- Start a new sale -----
        public void StartNewSale()
        {
            Console.Clear();
            cart.Clear();
            undoStack.Clear();
            receiptQueue.Clear();
            Console.WriteLine("New sale started. Cart is empty.");
        }

        // ----- Add item to cart -----
        public void AddItemToCart(int sku, int quantity)
        {
            if (products.productCatalog.TryGetValue(sku, out Product product))
            {
                if (product.Quantity < quantity)
                {
                    Console.WriteLine($"Insufficient stock. Only {product.Quantity} available.");
                    return;
                }

                var existingItem = cart.FirstOrDefault(c => c.Product == product);
                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                    undoStack.Push(() => { existingItem.Quantity -= quantity; });
                }
                else
                {
                    CartItem newItem = new CartItem { Product = product, Quantity = quantity };
                    cart.Add(newItem);
                    undoStack.Push(() => { cart.Remove(newItem); });
                }
                Console.WriteLine($"Added {quantity} x {product.Name} to cart.");
            }
            else
            {
                Console.WriteLine("Product not found!");
            }
        }

        // ----- Remove item by SKU -----
        public void RemoveItem(int sku)
        {
            var item = cart.FirstOrDefault(c => c.Product.Id == sku);
            if (item != null)
            {
                cart.Remove(item);
                Console.WriteLine($"Removed {item.Product.Name} from cart.");
            }
            else
            {
                Console.WriteLine("Item not found in cart.");
            }
        }

        // ----- Undo last action (Stack) -----
        public void UndoLastAction()
        {
            if (undoStack.Count > 0)
            {
                undoStack.Pop()();
                Console.WriteLine("Undo successful.");
            }
            else
            {
                Console.WriteLine("Nothing to undo.");
            }
        }

        // ----- Calculate subtotal -----
        public double CalculateSubtotal()
        {
            double subtotal = 0;
            foreach (var item in cart)
                subtotal += item.LineTotal;
            return subtotal;
        }

        // ----- Calculate tax using array lookup -----
        public double CalculateTax(double subtotal)
        {
            double rate = taxRates[0];
            for (int i = 0; i < taxThresholds.Length; i++)
            {
                if (subtotal >= taxThresholds[i])
                    rate = taxRates[i];
                else
                    break;
            }
            return subtotal * rate;
        }

        // ----- Grand total -----
        public double CalculateGrandTotal()
        {
            double sub = CalculateSubtotal();
            return sub + CalculateTax(sub);
        }

        // ----- Show cart contents -----
        public void ShowCart()
        {
            if (cart.Count == 0)
            {
                Console.WriteLine("Cart is empty.");
            }
            else
            {
                Console.WriteLine("Current Cart:");

                foreach (var item in cart)
                {
                    Console.WriteLine($"SKU: {item.Product.Id} | {item.Product.Name} | Qty: {item.Quantity} | Line Total: {item.LineTotal:C}");
                }
            }

            while (true)
            {
                Console.WriteLine("Press 0 to finish or 1 to go to main menu...");
                string select = Console.ReadLine();

                if (select == "0")
                {
                    Environment.Exit(0);
                }

                if (select == "1")
                {
                    Console.Clear();
                    Program.MainMenu();
                }
            }
        }

        // ----- Checkout: reduce stock, print receipt, enqueue -----
        public void Checkout()
        {
            if (cart.Count == 0)
            {
                Console.WriteLine("Cart is empty. Cannot checkout.");
                return;
            }

            foreach (var item in cart)
            {
                int newStock = item.Product.Quantity;
                if (!products.ReduceStock(item.Product.Id, item.Quantity, ref newStock))
                {
                    Console.WriteLine($"Failed to reduce stock for {item.Product.Name}. Aborting checkout.");
                    return;
                }
            }

            StringBuilder receipt = new StringBuilder();
            receipt.AppendLine("===== RECEIPT =====");
            receipt.AppendLine("QuickStop Mart");
            receipt.AppendLine("-------------------");
            foreach (var item in cart)
                receipt.AppendLine($"{item.Product.Name} x {item.Quantity} @ {item.Product.Price:C} = {item.LineTotal:C}");
            receipt.AppendLine("-------------------");
            double sub = CalculateSubtotal();
            double tax = CalculateTax(sub);
            double total = sub + tax;
            receipt.AppendLine($"Subtotal: {sub:C}");
            receipt.AppendLine($"Tax: {tax:C}");
            receipt.AppendLine($"Grand Total: {total:C}");
            receipt.AppendLine("Thank you for shopping!");
            receipt.AppendLine("===================");

            Console.WriteLine(receipt.ToString());
            receiptQueue.Enqueue(receipt.ToString());
            Console.WriteLine("Receipt added to print queue. Now press any key to go to main menu:");
            cart.Clear();
            undoStack.Clear();
        }

        // ----- Process the receipt queue (FIFO) -----
        public void ProcessReceiptQueue()
        {
            if (receiptQueue.Count == 0)
            {
                Console.WriteLine("No receipts in queue.");
                return;
            }
            while (receiptQueue.Count > 0)
            {
                string receipt = receiptQueue.Dequeue();
                Console.WriteLine("Processing receipt...");
                Console.WriteLine(receipt);
                while (true)
                {
                    Console.WriteLine("Receipt processed.Now press any key to go to main menu or 0 to exit:");
                    if (Console.ReadLine() == "0")
                    {
                        Environment.Exit(0);
                    }
                    else
                    {
                        Program.MainMenu();
                    }
                }
                
            }
        }
    }

    // ===== MAIN PROGRAM =====
    internal class Program
    {
        static ProductManager products = new ProductManager();
        static Sale currentSale = null;

        static async Task Main(string[] args)
        {
            // Load product catalog from file if exists
            await Task.Run(() => products.LoadCatalog());

            // Welcome message with StringBuilder
            StringBuilder text = new StringBuilder();
            text.AppendLine("\t\t\t\t\t\t===========================");
            text.AppendLine("\t\t\t\t\t\t|Welcome to Quickstop Mart|");
            text.AppendLine("\t\t\t\t\t\t===========================");
            Console.WriteLine(text);

            Console.WriteLine("Please enter your assigned credentials to log in!");
            while (true)
            {
                User employee = new User();
                Console.WriteLine("\nPlease enter your username!");
                employee.UserName = Console.ReadLine();
                Console.WriteLine("Please enter your password!");
                if (!int.TryParse(Console.ReadLine(), out int password))
                {
                    Console.WriteLine("Invalid password format. Try again.");
                    continue;
                }
                employee.Password = password;
                if (employee.UserName == "sam" && employee.Password == 123456)
                {
                    Console.WriteLine("You logged in successfully!");
                    Console.Clear();
                    MainMenu();
                    return;
                }
                else
                {
                    Console.WriteLine("Your provided info is wrong!");
                    Console.WriteLine("\nPress X to exit and any other key to try again!");
                    string choice = Console.ReadLine();
                    if (choice.ToLower() == "x")
                        break;
                }
            }
        }

        public static void MainMenu()
        {
            while (true)
            {
                Console.Clear();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("\t\t\t\t\t\t===========================");
                sb.AppendLine("\t\t\t\t\t\t|      Quickstop Mart     |");
                sb.AppendLine("\t\t\t\t\t\t===========================");
                Console.WriteLine(sb);
                Console.WriteLine("-----------------------");
                Console.WriteLine("|      Main Menu      |");
                Console.WriteLine("-----------------------");
                Console.WriteLine("\n1. Add Products");
                Console.WriteLine("2. View Products");
                Console.WriteLine("3. Start Sale (Add Items to Cart)");
                Console.WriteLine("4. Open Cart (View)");
                Console.WriteLine("5. Show Total");
                Console.WriteLine("6. Checkout");
                Console.WriteLine("7. Undo Last Action");
                Console.WriteLine("8. Process Receipt Queue");
                Console.WriteLine("0. Exit");
                Console.WriteLine("\nPlease select an option!");

                string input = Console.ReadLine();
                if (!int.TryParse(input, out int select))
                {
                    Console.WriteLine("Invalid input. Press any key to continue...");
                    Console.ReadKey();
                    continue;
                }

                switch (select)
                {
                    case 1:
                        Console.Clear();
                        products.AddProducts();
                        break;
                    case 2:
                        Console.Clear();
                        products.ViewProducts();
                        break;
                    case 3:
                        currentSale = new Sale(products);
                        currentSale.StartNewSale();
                        AddItemsLoop();
                        break;
                    case 4:
                        if (currentSale == null)
                        {
                            Console.WriteLine("No active sale. Please start a sale first (option 3).");
                            Console.ReadKey();
                            break;
                        }
                        Console.Clear();
                        currentSale.ShowCart();
                        Console.ReadKey();
                        break;
                    case 5:
                        if (currentSale == null)
                        {
                            Console.WriteLine("No active sale.");
                            Console.ReadKey();
                            break;
                        }
                        Console.Clear();
                        Console.WriteLine($"Subtotal: {currentSale.CalculateSubtotal():C}");
                        Console.WriteLine($"Tax: {currentSale.CalculateTax(currentSale.CalculateSubtotal()):C}");
                        Console.WriteLine($"Grand Total: {currentSale.CalculateGrandTotal():C}");

                        while (true)
                        {
                            Console.WriteLine("Press 0 to finish or 1 to go to main menu...");
                            string totalChoice = Console.ReadLine();

                            if (totalChoice == "0")
                            {
                                Environment.Exit(0);
                            }

                            if (totalChoice == "1")
                            {
                                Console.Clear();
                                break;
                            }
                        }
                        break;
                    case 6:
                        if (currentSale == null)
                        {
                            Console.WriteLine("No active sale.");
                            Console.ReadKey();
                            break;
                        }
                        Console.Clear();
                        currentSale.Checkout();
                        Console.ReadKey();
                        break;
                    case 7:
                        if (currentSale == null)
                        {
                            Console.WriteLine("No active sale.");
                            Console.ReadKey();
                            break;
                        }
                        currentSale.UndoLastAction();
                        Console.ReadKey();
                        break;
                    case 8:
                        Console.Clear();
                        if (currentSale == null)
                        {
                            Console.WriteLine("No sale to process receipts.");
                            Console.ReadKey();
                            break;
                        }
                        currentSale.ProcessReceiptQueue();
                        Console.ReadKey();
                        break;
                    case 0:
                        Console.WriteLine("Goodbye!");
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Invalid option. Press any key...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        // ===== Helper loop for adding items during a sale =====
        static void AddItemsLoop()
        {
            while (true)
            {
                Console.WriteLine("\n--- Add Items to Cart ---");
                Console.WriteLine("Enter SKU (Product ID) to add (or 0 to finish / m to go to main menu): ");
                string skuInput = Console.ReadLine();

                if (skuInput.ToLower() == "m")
                {
                    Console.Clear();
                    return;
                }

                if (!int.TryParse(skuInput, out int sku))
                {
                    Console.WriteLine("Invalid SKU.");
                    continue;
                }

                if (sku == 0)
                {
                    Environment.Exit(0);
                }

                if (products.productCatalog.TryGetValue(sku, out Product product))
                {
                    Console.WriteLine($"Product found: {product.Name} | Price: {product.Price:C} | Stock: {product.Quantity}");
                    Console.Write("Add to cart? (y/n): ");
                    string answer = Console.ReadLine();

                    while (answer.ToLower() != "y" && answer.ToLower() != "n")
                    {
                        Console.WriteLine("Invalid input! Please enter only y or n.");
                        Console.Write("Add to cart? (y/n): ");
                        answer = Console.ReadLine();
                    }

                    if (answer.ToLower() == "y")
                    {
                        Console.Write("Enter quantity: ");

                        if (int.TryParse(Console.ReadLine(), out int qty) && qty > 0)
                        {
                            currentSale.AddItemToCart(sku, qty);
                        }
                        else
                        {
                            Console.WriteLine("Invalid quantity.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Product not found.");
                }

                Console.WriteLine("Press any key to continue adding, or 0 to finish / m to go to main menu...");
                string more = Console.ReadLine();

                if (more == "0")
                    break;

                if (more.ToLower() == "m")
                {
                    Console.Clear();
                    return;
                }
            }

            Console.WriteLine("Finished adding items. Returning to main menu.");
        }
    }
}