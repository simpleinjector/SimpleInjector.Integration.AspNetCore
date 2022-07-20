using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AspNetCoreRazorPages50PureDI.Pages
{
    public class DisposableObject : IDisposable
    {
        public void Dispose()
        {
        }
    }

    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger, CommerceContext obj)
        {
            _logger = logger;
        }

        public void OnGet()
        {

        }
    }
}
