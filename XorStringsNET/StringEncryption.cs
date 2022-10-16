using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.IO;
using System.Linq;

namespace XorStringsNET;

public class StringEncryption
{
    private readonly EncryptionService _encryptionService;
    private readonly ModuleDefMD _module;

    private FieldDef _arrayPtrField = null!;

    private MethodDef _decryptionMethod = null!;

    private MethodDef _placeholderMethod = null!;

    public StringEncryption(ModuleDefMD module)
    {
        _module = module;
        _encryptionService = new EncryptionService();
    }

    public ModuleDefMD Run()
    {
        // Inject the runtime which contains our decryption method
        InjectRuntime();

        // Process CIL method bodies to find and encrypt all strings
        ProcessModule();

        // Prepare the struct with the encrypted data
        SetupStruct();

        // Patch the placeholder values in the runtime
        PatchRuntimePlaceholders();

        return this._module;
    }

    private void InjectRuntime()
    {
        // Load the runtime module
        string baseDirectory = AppContext.BaseDirectory;
        var implantModule = ModuleDefMD.Load(Path.Combine(baseDirectory, "Runtime.dll"));

        // Initialize a new instance of the member cloner for the target module

        TypeDef loader = implantModule.ResolveTypeDef(MDToken.ToRID(typeof(Runtime.Runtime).MetadataToken));
        
        loader.Namespace = string.Empty;
        
        implantModule.Types.Remove(loader);
        this._module.Types.Add(loader);

        _decryptionMethod = loader.Methods.First(m => m.Name == "Decrypt");

        _decryptionMethod.Name = GetGuidString();
        _decryptionMethod.DeclaringType!.Name = GetGuidString();

        _placeholderMethod = (MethodDef)loader.Methods.First(m => m.Name == "cpblk");
    }

    private void ProcessModule()
    {
        // Go trough all types that have at least one method
        //foreach (var type in _module.GetAllTypes().Where(t => t.Methods.Count > 0))
        foreach (var type in _module.Types.Where(t => t.Methods.Count > 0))
        {
            // Skip this type since its the injected runtime class
            if (type == _decryptionMethod.DeclaringType)
                continue;

            // Go trough all methods of the type
            foreach (var method in type.Methods)
            {
                // Skip non CIL methods
                if (method.Body == null)
                    continue;

                // Iterate over the method bodies CIL instructions
                var instructions = method.Body.Instructions;
                for (int i = 0; i < instructions.Count; i++)
                {
                    // Find strings
                    if (instructions[i].OpCode != OpCodes.Ldstr)
                        continue;

                    if (instructions[i].Operand == null)
                        continue;

                    // Since empty strings cannot be encrypted, give them a negative id so the runtime
                    // can handle them separately
                    if ((string)instructions[i].Operand! == string.Empty)
                    {
                        // Negate index for empty strings, the runtime will handle negative index values in a special way
                        instructions[i].OpCode = OpCodes.Ldc_I4;
                        instructions[i].Operand = -(_encryptionService.Index);

                        instructions.Insert(i + 1, new Instruction(OpCodes.Call, _decryptionMethod));
                        continue;
                    }

                    _encryptionService.Encrypt((string)instructions[i].Operand!);

                    // Replace the string assignment with the encrypted id (index)
                    instructions[i].OpCode = OpCodes.Ldc_I4;
                    instructions[i].Operand = _encryptionService.Index;

                    instructions.Insert(i + 1, new Instruction(OpCodes.Call, _decryptionMethod));
                }

                instructions.OptimizeMacros();
            }
        }
    }

    private void SetupStruct()
    {
        var staticStruct = _decryptionMethod.DeclaringType?.NestedTypes[0];

        // Apply the necessary attributes
        staticStruct!.Attributes = TypeAttributes.ExplicitLayout | TypeAttributes.BeforeFieldInit
                                                                 | TypeAttributes.NestedPrivate;

        staticStruct.Name = GetGuidString();

        // Add new field based on the struct
        var field = new FieldDefUser(GetGuidString(), new FieldSig(staticStruct.ToTypeSig()), FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.HasFieldRVA);

        _decryptionMethod.DeclaringType?.Fields.Add(field);

        _arrayPtrField = field;

        // Add data to FieldRva and add ClassLayout with size being the length of the encrypted data
        staticStruct.ClassLayout = new ClassLayoutUser(1, _encryptionService.Length);
        field.InitialValue = _encryptionService.Data;//new DataSegment(_encryptionService.Data);
    }

    private void PatchRuntimePlaceholders()
    {
        var instructions = _decryptionMethod.Body?.Instructions;

        if (instructions == null)
            throw new ArgumentNullException();

        // Patch the placeholder for the data address with the field containing the fieldrva
        //var patch = instructions?.First(i => i.IsLdcI4() && i.GetLdcI4Constant() == 0x420);
        var patch = instructions?.First(i => i.IsLdcI4() && i.GetLdcI4Value() == 0x420);

        patch.OpCode = OpCodes.Ldsflda;
        patch.Operand = _arrayPtrField;

        // Replace the call to the cpblk placeholder method with the actual cpblk CIL instruction
        patch = instructions?.First(i => i.Operand == _placeholderMethod);

        patch.OpCode = OpCodes.Cpblk;

        _decryptionMethod.DeclaringType!.Methods.Remove(_placeholderMethod);
    }

    private static string GetGuidString() => Guid.NewGuid().ToString().ToUpper();
}