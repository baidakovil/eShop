// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace IdentityServerHost.Quickstart.UI;

[Authorize]
[SecurityHeaders]
public class DeviceController : Controller
{
    private readonly IDeviceAuthorizationWorkflowService _deviceAuthorizationWorkflowService;
    private readonly IOptions<IdentityServerOptions> _options;

    public DeviceController(
        IDeviceAuthorizationWorkflowService deviceAuthorizationWorkflowService,
        IOptions<IdentityServerOptions> options)
    {
        _deviceAuthorizationWorkflowService = deviceAuthorizationWorkflowService;
        _options = options;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string userCodeParamName = _options.Value.UserInteraction.DeviceVerificationUserCodeParameter;
        string userCode = Request.Query[userCodeParamName];
        if (string.IsNullOrWhiteSpace(userCode)) return View("UserCodeCapture");

        var vm = await _deviceAuthorizationWorkflowService.BuildViewModelAsync(userCode);
        if (vm == null) return View("Error");

        vm.ConfirmUserCode = true;
        return View("UserCodeConfirmation", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UserCodeCapture(string userCode)
    {
        var vm = await _deviceAuthorizationWorkflowService.BuildViewModelAsync(userCode);
        if (vm == null) return View("Error");

        return View("UserCodeConfirmation", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Callback(DeviceAuthorizationInputModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var result = await _deviceAuthorizationWorkflowService.ProcessConsentAsync(model, User);
        if (result.HasValidationError) return View("Error");

        return View("Success");
    }
}
