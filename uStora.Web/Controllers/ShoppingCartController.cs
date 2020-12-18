﻿using AutoMapper;
using Microsoft.AspNet.Identity;
using MvcPaging;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using uStora.Common;
using uStora.Model.Models;
using uStora.Service;
using uStora.Web.App_Start;
using uStora.Web.Infrastructure.Extensions;
using uStora.Web.Infrastructure.NganLuongAPI;
using uStora.Web.Models;
using PayPal.Api;
using System;
using Order = uStora.Model.Models.Order;

namespace uStora.Web.Controllers
{
    public class ShoppingCartController : Controller
    {
        private IProductService _productService;
        private IOrderService _orderService;
        private ApplicationUserManager _userManager;

        private string merchantId = ConfigHelper.GetByKey("MerchantId");
        private string merchantPassword = ConfigHelper.GetByKey("MerchantPassword");
        private string merchantEmail = ConfigHelper.GetByKey("MerchantEmail");


        public ShoppingCartController(IProductService productService,
            ApplicationUserManager userManager, IOrderService orderService)
        {
            _productService = productService;
            _userManager = userManager;
            _orderService = orderService;
        }

        // GET: ShoppingCart
        public ActionResult Index(string searchString, int? page)
        {
            if (Session[CommonConstants.ShoppingCartSession] == null)
            {
                Session[CommonConstants.ShoppingCartSession] = new List<ShoppingCartViewModel>();
            }
            var cart = new ShoppingCartViewModel();
            int defaultPageSize = int.Parse(ConfigHelper.GetByKey("pageSizeAjax"));
            CommonController common = new CommonController(_productService);
            int currentPageIndex = page.HasValue ? page.Value : 1;
            cart.ListProducts = common.ProductListAjax(page, searchString).ToPagedList(currentPageIndex, defaultPageSize);
            if (Request.IsAjaxRequest())
                return PartialView("_AjaxProductList", cart.ListProducts);
            else
                return View(cart);
        }
        [Authorize]
        public JsonResult GetUserInfo()
        {
            if (Request.IsAuthenticated)
            {
                var userId = User.Identity.GetUserId();
                var user = _userManager.FindById(userId);
                return Json(new
                {
                    data = user,
                    status = true
                });
            }
            return Json(new
            {
                status = false,
                message = "Bạn cần đăng nhập để sử dụng tính năng này!!!"
            });
        }

        public JsonResult GetAll()
        {
            if (Session[CommonConstants.ShoppingCartSession] == null)
            {
                Session[CommonConstants.ShoppingCartSession] = new List<ShoppingCartViewModel>();
            }
            var cart = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];

            return Json(new
            {
                status = true,
                data = cart
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Add(long productId)
        {
            var cart = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];
            var product = _productService.FindById(productId);
            if (cart == null)
            {
                cart = new List<ShoppingCartViewModel>();
            }

            if (product.Quantity == 0)
            {
                return Json(new
                {
                    status = false,
                    message = "Sản phẩm này hiện tại đang hết hàng."
                });
            }
            if (cart.Any(x => x.ProductId == productId))
            {
                foreach (var item in cart)
                {
                    if (item.ProductId == productId)
                        item.Quantity += 1;
                }
            }
            else
            {
                ShoppingCartViewModel newItem = new ShoppingCartViewModel();
                newItem.ProductId = productId;
                newItem.Product = Mapper.Map<Product, ProductViewModel>(product);
                newItem.Quantity = 1;
                cart.Add(newItem);
            }
            Session[CommonConstants.ShoppingCartSession] = cart;
            Session[CommonConstants.SelledProducts] = cart;
            return Json(new
            {
                status = true
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public JsonResult Update(string cartData)
        {
            var cartViewModel = new JavaScriptSerializer().Deserialize<List<ShoppingCartViewModel>>(cartData);
            var cartSession = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];
            foreach (var item in cartSession)
            {
                foreach (var jitem in cartViewModel)
                {
                    if (item.ProductId == jitem.ProductId)
                    {
                        item.Quantity = jitem.Quantity;
                    }
                }
            }

            Session[CommonConstants.ShoppingCartSession] = cartSession;

            return Json(new
            {
                status = true
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public JsonResult DeleteItem(int productId)
        {
            var cartSession = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];
            if (cartSession != null)
            {
                cartSession.RemoveAll(x => x.ProductId == productId);
                return Json(new
                {
                    status = true
                });
            }
            return Json(new
            {
                status = false
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public JsonResult DeleteAll()
        {
            Session[CommonConstants.ShoppingCartSession] = new List<ShoppingCartViewModel>();

            return Json(new
            {
                status = true
            });
        }

        [HttpPost]
        public ActionResult CreateOrder(string orderViewModel)
        {
            if (!Request.IsAuthenticated)
            {
                TempData["UnAuthenticated"] = "Bạn phải đăng nhập để thanh toán";
                return Json(new { status = false });
            }
            var order = new JavaScriptSerializer().Deserialize<OrderViewModel>(orderViewModel);
            var orderNew = new Order();
            bool isEnough = true;
            orderNew.UpdateOrder(order);
            if (Request.IsAuthenticated)
            {
                var userId = User.Identity.GetUserId();
                orderNew.CustomerId = userId;
                orderNew.CreatedBy = User.Identity.GetUserName();
            }
            var cart = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];
            List<OrderDetail> orderDetails = new List<OrderDetail>();
            foreach (var item in cart)
            {
                var detail = new OrderDetail();
                detail.ProductID = item.ProductId;
                detail.Quantity = item.Quantity;
                detail.Price = item.Product.Price;
                orderDetails.Add(detail);
                isEnough = _productService.SellingProduct(item.ProductId, item.Quantity);
            }
            if (isEnough)
            {
                var orderReturn = _orderService.Add(ref orderNew, orderDetails);
                _productService.SaveChanges();
                if (order.PaymentMethod == "CASH")
                {
                    return Json(new
                    {
                        status = true
                    });
                }
                else
                {
                    var currentLink = ConfigHelper.GetByKey("CurrentLink");
                    RequestInfo info = new RequestInfo();
                    info.Merchant_id = merchantId;
                    info.Merchant_password = merchantPassword;
                    info.Receiver_email = merchantEmail;



                    info.cur_code = "vnd";
                    info.bank_code = order.BankCode;

                    info.Order_code = orderReturn.ID.ToString();
                    info.Total_amount = orderDetails.Sum(x => x.Quantity * x.Price).ToString();
                    info.fee_shipping = "0";
                    info.Discount_amount = "0";
                    info.order_description = "Thanh toán đơn hàng tại uStora shop";
                    info.return_url = currentLink + "/xac-nhan-don-hang.htm";
                    info.cancel_url = currentLink + "/huy-don-hang.htm";

                    info.Buyer_fullname = order.CustomerName;
                    info.Buyer_email = order.CustomerEmail;
                    info.Buyer_mobile = order.CustomerMobile;

                    APICheckout objNLChecout = new APICheckout();
                    ResponseInfo result = objNLChecout.GetUrlCheckout(info, order.PaymentMethod);
                    if (result.Error_code == "00")
                    {
                        return Json(new
                        {
                            status = true,
                            urlCheckout = result.Checkout_url,
                            message = result.Description
                        });
                    }
                    else
                        return Json(new
                        {
                            status = false,
                            message = result.Description
                        });
                }
            }
            else
            {
                return Json(new
                {
                    status = false,
                    message = "Sản phẩm này hiện tại đang hết hàng."
                });
            }

        }

        [Authorize]
        public ActionResult CheckOutSuccess()
        {
            var userId = User.Identity.GetUserId();
            var orders = _orderService.GetListOrders(User.Identity.GetUserId());
            ViewBag.isNull = false;
            if (orders.Count() == 0)
                ViewBag.isNull = true;
            return View(orders);
        }

        [Authorize]
        public JsonResult GetListOrder()
        {
            var userId = User.Identity.GetUserId();
            var orders = _orderService.GetListOrders(User.Identity.GetUserId());

            return Json(new
            {
                data = orders,
                status = true
            }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult ConfirmOrder()
        {
            string token = Request["token"];
            RequestCheckOrder info = new RequestCheckOrder();
            info.Merchant_id = merchantId;
            info.Merchant_password = merchantPassword;
            info.Token = token;
            APICheckout objNLChecout = new APICheckout();
            ResponseCheckOrder result = objNLChecout.GetTransactionDetail(info);
            if (result.errorCode == "00")
            {
                _orderService.UpdateStatus(int.Parse(result.order_code));
                _orderService.SaveChanges();
                ViewBag.IsSuccess = true;
                ViewBag.Result = "Thanh toán thành công. Chúng tôi sẽ liên hệ lại sớm nhất.";
            }
            else
            {
                ViewBag.IsSuccess = false;
                ViewBag.Result = "Có lỗi xảy ra. Vui lòng liên hệ admin.";
            }
            return View();
        }
        public ActionResult CancelOrder()
        {
            return View();
        }

        // Paypal
        private Payment payment;

        // Create a paypment using an APIContext
        private Payment CreatePayment(APIContext apiContext, string redirectUrl)
        {
            var listItems = new ItemList() { items = new List<Item>() };

            List<ShoppingCartViewModel> listCarts = (List<ShoppingCartViewModel>)Session[CommonConstants.ShoppingCartSession];
            foreach (var cart in listCarts)
            {
                listItems.items.Add(new Item()
                {
                    name = cart.Product.Name,
                    currency = "USD",
                    price = cart.Product.Price.ToString(),
                    quantity = cart.Quantity.ToString(),
                    sku = "sku"
                });
            }

            var payer = new Payer() { payment_method = "paypal" };

            // Do the configuration RedirectURLs here with redirectURLs object
            var redirUrls = new RedirectUrls()
            {
                cancel_url = redirectUrl,
                return_url = redirectUrl
            };

            // Create details object
            var details = new Details()
            {
                tax = "1",
                shipping = "2",
                subtotal = listCarts.Sum(x => x.Quantity * x.Product.Price).ToString()
            };

            // Create amount object
            var amount = new Amount()
            {
                currency = "USD",
                total = (Convert.ToDouble(details.tax) + Convert.ToDouble(details.shipping) + Convert.ToDouble(details.subtotal)).ToString(),// tax + shipping + subtotal
                details = details
            };

            // Create transaction
            var transactionList = new List<Transaction>();
            transactionList.Add(new Transaction()
            {
                description = "Chien Testing transaction description",
                invoice_number = Convert.ToString((new Random()).Next(100000)),
                amount = amount,
                item_list = listItems
            });

            payment = new Payment()
            {
                intent = "sale",
                payer = payer,
                transactions = transactionList,
                redirect_urls = redirUrls
            };

            return payment.Create(apiContext);
        }

        // Create ExecutePayment method
        private Payment ExecutePayment(APIContext apiContext, string payerId, string paymentId)
        {
            var paymentExecution = new PaymentExecution()
            {
                payer_id = payerId
            };
            payment = new Payment() { id = paymentId };
            return payment.Execute(apiContext, paymentExecution);
        }

        // Create PaymentWithPaypal method
        public ActionResult PaymentWithPaypal()
        {
            // Gettings context from the paypal bases on clientId and clientSecret for payment
            APIContext apiContext = PaypalConfiguration.GetAPIContext();

            try
            {
                string payerId = Request.Params["PayerID"];
                if (string.IsNullOrEmpty(payerId))
                {
                    // Creating a payment
                    string baseURI = Request.Url.Scheme + "://" + Request.Url.Authority + "/ShoppingCart/PaymentWithPaypal?";
                    var guid = Convert.ToString((new Random()).Next(100000));
                    var createdPayment = CreatePayment(apiContext, baseURI + "guid=" + guid);

                    // Get links returned from paypal response to create call funciton
                    var links = createdPayment.links.GetEnumerator();
                    string paypalRedirectUrl = string.Empty;

                    while (links.MoveNext())
                    {
                        Links link = links.Current;
                        if (link.rel.ToLower().Trim().Equals("approval_url"))
                        {
                            paypalRedirectUrl = link.href;
                        }
                    }

                    Session.Add(guid, createdPayment.id);
                    return Redirect(paypalRedirectUrl);
                }
                else
                {
                    // This one will be executed when we have received all the payment params from previous call
                    var guid = Request.Params["guid"];
                    var executedPayment = ExecutePayment(apiContext, payerId, Session[guid] as string);
                    if (executedPayment.state.ToLower() != "approved")
                    {
                        //Remove shopping cart session
                        //Session.Remove(strCart);
                        return View("Failure");
                    }
                }
            }
            catch (Exception ex)
            {
                PaypalLogger.Log("Error: " + ex.Message);
                //Remove shopping cart session
                //Session.Remove(strCart);
                return View("Failure");
            }

            //Remove shopping cart session
            //Session.Remove(strCart);
            return View("Success");
        }
    }
}