// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace IdentityServerHost.Quickstart.UI;

/// <summary>
/// This controller processes the consent UI
/// </summary>
[SecurityHeaders]
[Authorize]
public class ConsentController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IConsentWorkflowService _consentWorkflowService;

    public ConsentController(
        IIdentityServerInteractionService interaction,
        IConsentWorkflowService consentWorkflowService)
    {
        _interaction = interaction;
        _consentWorkflowService = consentWorkflowService;
    }

    /// <summary>
    /// Shows the consent screen
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> Index(string returnUrl)
    {
        var vm = await _consentWorkflowService.BuildViewModelAsync(returnUrl);
        if (vm != null)
        {
            return View("Index", vm);
        }

        return View("Error");
    }

    /// <summary>
    /// Handles the consent screen postback
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ConsentInputModel model)
    {
        var result = await _consentWorkflowService.ProcessConsentAsync(model, User);

        if (result.IsRedirect)
        {
            var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);
            if (context?.IsNativeClient() == true)
            {
                // The client is native, so this change in how to
                // return the response is for better UX for the end user.
                return this.LoadingPage("Redirect", result.RedirectUri);
            }

            return Redirect(result.RedirectUri);
        }

        if (result.HasValidationError)
        {
            ModelState.AddModelError(string.Empty, result.ValidationError);
        }

        if (result.ShowView)
        {
            return View("Index", result.ViewModel);
        }

        return View("Error");
    }

    /*****************************************/
    /* helper APIs for the ConsentController */
    /*****************************************/
}
