using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Net48Sample.Pages;

public class IndexModel : PageModel
{
    [Route("/")]
    public ActionResult OnGet()
    {
        return Redirect("/ui/graphql");
    }
}
