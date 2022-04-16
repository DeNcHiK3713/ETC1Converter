using AssetsTools.NET;
using AssetsTools.NET.Extra;
using ETC1Converter;

var startDirectory = Path.Combine(Directory.GetCurrentDirectory(), "astc");
var files = Directory.GetFiles(startDirectory, "*.unity3d", SearchOption.TopDirectoryOnly);

var assetManager = new AssetsManager();
assetManager.LoadClassPackage("classdata.tpk");

foreach (var file in files)
{
    var bundle = assetManager.LoadBundleFile(file);
    //var compressionType = (AssetBundleCompressionType)bundle.file.bundleHeader6.GetCompressionType();
    var compressionType = AssetBundleCompressionType.LZ4;
    var bundleReplacers = new List<BundleReplacer>();
    
    foreach (var info in bundle.file.bundleInf6.dirInf)
    {
        var assetInstance = assetManager.LoadAssetsFileFromBundle(bundle, info.name, true);
        if (assetInstance is null)
        {
            continue;
        }
        var assetsReplacers = new List<AssetsReplacer>();
        assetManager.LoadClassDatabaseFromPackage(assetInstance.file.typeTree.unityVersion);

        var textures = assetInstance.table.GetAssetsOfType((int)AssetClassID.Texture2D);
        foreach (var texture in textures)
        {
            var baseField = assetManager.GetTypeInstance(assetInstance.file, texture).GetBaseField();
            var textureFile = TextureFile.ReadTextureFile(baseField);
            var format = (TextureFormat)textureFile.m_TextureFormat;

            if (format == TextureFormat.ASTC_RGB_6x6)
            {
                var newFormat = TextureFormat.ETC_RGB4;
                var rawData = TextureHelper.GetRawTextureBytes(textureFile, bundle.file);
                var data = TextureEncoderDecoder.Decode(rawData, textureFile.m_Width, textureFile.m_Height, format);
                Thread.Sleep(10);
                var newData = TextureEncoderDecoder.Encode(data, textureFile.m_Width, textureFile.m_Height, newFormat);
                Thread.Sleep(10);
                TextureHelper.SetRawTextureBytes(textureFile, baseField, newData, newFormat);

                var newGoBytes = baseField.WriteToByteArray();
                var replacer = new AssetsReplacerFromMemory(0, texture.index, (int)texture.curFileType, 0xffff, newGoBytes);

                assetsReplacers.Add(replacer);
            }
        }

        if (assetsReplacers.Any())
        {
            byte[] newAssetData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInstance.file.Write(writer, 0, assetsReplacers, 0);
                newAssetData = stream.ToArray();
            }

            var bundleReplacer = new BundleReplacerFromMemory(info.name, null, true, newAssetData, -1);
            bundleReplacers.Add(bundleReplacer);
        }
    }

    if (bundleReplacers.Any())
    {        
        using var stream = new MemoryStream();
        using var bundleMemoryWriter = new AssetsFileWriter(stream);
        bundle.file.Write(bundleMemoryWriter, bundleReplacers);
        bundleMemoryWriter.Position = 0;
        using var bundleMemoryReader = new AssetsFileReader(bundleMemoryWriter.BaseStream);
        using var bundleFileWriter = new AssetsFileWriter(file);
        bundle.file.Pack(bundleMemoryReader, bundleFileWriter, compressionType);
    }

    assetManager.UnloadAll();
    Console.WriteLine($"{file} processed...");
}