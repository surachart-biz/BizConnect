using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.ViewModels.Components
{
    public class FormFieldViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string FieldType { get; set; } = "text";
        public string PlaceholderText { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int Rows { get; set; } = 3;
        public List<SelectListItem>? Options { get; set; }
        public bool Required { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public bool ReadOnly { get; set; } = false;
        public string ContainerClass { get; set; } = "mb-3";
        public string InputClass { get; set; } = "";
        public string Icon { get; set; } = "";
    }
}