﻿using System.ComponentModel.DataAnnotations;
using LykkePay.Common.Validation;

namespace Lykke.Service.PayAuth.Models.Employees
{
    public class UpdateCredentialsModel
    {
        [Required]
        public string EmployeeId { get; set; }

        [Required]
        public string MerchantId { get; set; }

        [Required]
        [EmailAndRowKey]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
