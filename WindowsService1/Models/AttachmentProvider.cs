using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService1.Models
{
    public class AttachmentProvider
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public long? PaymentRequestID { get; set; }
        public long? PurchaseOrderID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedDt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedDt { get; set; }
        public bool? Status { get; set; }
        public string TypeAttachment { get; set; }
        public long? RefundOrderID { get; set; }
        public long? BillingOrderID { get; set; }
        public string BaseUrl { get; set; }
        public long? EmployeeID { get; set; }
    }
}
