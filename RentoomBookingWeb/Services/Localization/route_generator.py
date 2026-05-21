import os
import xml.etree.ElementTree as ET
import re
import unicodedata

# Configuration
RES_DIR = '../../Resources'
OUTPUT_FILE = 'LocalizedRouteRegistry.cs'
NAMESPACE = 'RentoomBookingWeb.Services.Localization'

# Mapping: Key -> { RES_FILE_PREFIX, RES_KEY, COMPONENT_FILE, PARAMS }
PAGE_MAPPINGS = {
    'Home': {
        'file_prefix': 'HomePage', 
        'res_key': 'Home_PageTitle',
        'file_path': '../../Components/Features/Home/Pages/Home.razor',
        'params': '',
        'is_home': True
    },
    'Apartments': {
        'file_prefix': 'Apartments', 
        'res_key': 'ApartmentsText',
        'file_path': '../../Components/Features/Apartments/Pages/Apartments.razor',
        'params': ''
    },
    'Contact': {
        'file_prefix': 'Contact', 
        'res_key': 'ContactText',
        'file_path': '../../Components/Features/Contact/Pages/Contact.razor',
        'params': ''
    },
    'Cooperation': {
        'file_prefix': 'Cooperation', 
        'res_key': 'CooperationText',
        'file_path': '../../Components/Features/Cooperation/Pages/Cooperation.razor',
        'params': ''
    },
    'Statute': {
        'file_prefix': 'StatutePage', 
        'res_key': 'StatuteHeader',
        'file_path': '../../Components/Features/Statute/Pages/Statute.razor',
        'params': '/{Id:int}/{Slug?}'
    },
    'AboutCity': {
        'file_prefix': 'SharedResources', 
        'res_key': 'AboutCity',
        'file_path': '../../Components/Features/TorunLocation/Pages/TorunLocation.razor',
        'params': ''
    },
    'ApartmentDetail': {
        'file_prefix': 'Apartments', 
        'res_key': 'ApartmentsText',
        'file_path': '../../Components/Features/ReservationWorkflow/Pages/ApartmentPage.razor',
        'params': '/{id:int}/{slug}/{startDate?}/{endDate?}/{adults?}/{children?}'
    }
}

def to_slug(phrase):
    if not phrase:
        return ""
    nks = unicodedata.normalize('NFKD', phrase)
    res = "".join([c for c in nks if not unicodedata.combining(c)])
    res = res.lower()
    res = re.sub(r'[^a-z0-9]+', '-', res)
    return res.strip('-')

def extract_translations():
    mapping = {}
    files = [f for f in os.listdir(RES_DIR) if f.endswith('.resx')]
    
    # Collect all unique cultures first
    all_cultures = set()
    for f in files:
        # Match pattern: Prefix(.Culture)?.resx
        # We need a generic way to find all cultures used in the project
        match = re.match(r'^.*\.([a-z-]+)\.resx$', f, re.IGNORECASE)
        if match:
            all_cultures.add(match.group(1).lower())
    all_cultures.add("pl")

    for key, config in PAGE_MAPPINGS.items():
        mapping[key] = {}
        prefix = config['file_prefix']
        res_key = config['res_key']
        
        # Initialize with empty for all cultures
        for cult in all_cultures:
            mapping[key][cult] = ""

        pattern = re.compile(rf"^{prefix}(\.([a-z-]+))?\.resx$", re.IGNORECASE)
        
        for f in files:
            match = pattern.match(f)
            if match:
                culture = match.group(2) or "pl"
                culture = culture.lower()
                
                if config.get('is_home'):
                    mapping[key][culture] = ""
                    continue

                try:
                    tree = ET.parse(os.path.join(RES_DIR, f))
                    root = tree.getroot()
                    
                    for data in root.findall('data'):
                        if data.get('name') == res_key:
                            val_node = data.find('value')
                            if val_node is not None and val_node.text:
                                mapping[key][culture] = to_slug(val_node.text)
                            break
                except Exception as e:
                    print(f"Error parsing {f}: {e}")
                    
        # Special case for ApartmentDetail
        if key == 'ApartmentDetail':
            for cult in all_cultures:
                if not mapping[key].get(cult) and mapping.get('Apartments', {}).get(cult):
                    mapping[key][cult] = mapping['Apartments'][cult]
                    
    return mapping

def generate_csharp(mapping):
    lines = []
    lines.append("using System;")
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append(f"namespace {NAMESPACE}")
    lines.append("{")
    lines.append("    public static class LocalizedRouteRegistry")
    lines.append("    {")
    lines.append("        public static readonly Dictionary<string, Dictionary<string, string>> PageSlugs = new()")
    lines.append("        {")
    
    for key in sorted(mapping.keys()):
        lines.append(f'            ["{key}"] = new(StringComparer.OrdinalIgnoreCase)')
        lines.append("            {")
        for culture in sorted(mapping[key].keys()):
            slug = mapping[key][culture]
            lines.append(f'                ["{culture}"] = "{slug}",')
        lines.append("            },")
        
    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    return "\n".join(lines)

def patch_razor_files(mapping):
    print("Patching Razor files...")
    start_marker = "@* [SEO_ROUTES] *@"
    end_marker = "@* [SEO_ROUTES_END] *@"
    
    all_cultures = set()
    for k in mapping:
        all_cultures.update(mapping[k].keys())

    for key, config in PAGE_MAPPINGS.items():
        file_path = config['file_path']
        if not os.path.exists(file_path):
            continue
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        if start_marker not in content or end_marker not in content:
            continue
            
        route_lines = set()
        cultures = mapping.get(key, {})
        
        # Polish fallback is critical to avoid ambiguous routes
        polish_slug = cultures.get('pl', cultures.get('pl-pl', ''))
        
        is_home = config.get('is_home', False)
        params = config.get('params', '')

        for culture in all_cultures:
            short_culture = culture.split('-')[0].lower()
            slug = cultures.get(culture, "")
            
            # If slug is empty and it's NOT home page, use Polish slug
            if not is_home and not slug:
                slug = polish_slug
            
            if not slug:
                route_lines.add(f'@page "/{short_culture}{params}"')
            else:
                route_lines.add(f'@page "/{short_culture}/{slug}{params}"')
            
        sorted_routes = sorted(list(route_lines))
        replacement_block = start_marker + "\n" + "\n".join(sorted_routes) + "\n" + end_marker
        pattern = re.compile(rf"{re.escape(start_marker)}.*?{re.escape(end_marker)}", re.DOTALL)
        new_content = pattern.sub(replacement_block, content)
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Patched {file_path}")

if __name__ == "__main__":
    mapping = extract_translations()
    csharp_code = generate_csharp(mapping)
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        f.write(csharp_code)
    patch_razor_files(mapping)
    print("Done!")
