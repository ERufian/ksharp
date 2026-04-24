// Complete FFI Workflow Test
// This test covers the full FFI functionality from assembly loading to method invocation

// Step 1: Load .NET assembly with Complex type
complex:"C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\8.0.26\\System.Runtime.Numerics.dll" 2: `System.Numerics.Complex

// Step 2: Create constructor function
complex_new:complex[`constructor]

// Step 3: Create object instance using constructor
c1:complex_new[2;3]

// Step 4: Call instance method (Abs - magnitude)
magnitude: c1[`Abs][]

// Step 5: Call property getter methods 
real: c1[`Real][]
imag: c1[`Imaginary]

// Step 6: Call static method (Conjugate)
conj_func: ._dotnet.System.Numerics.Complex.Conjugate
conj_result: conj_func[c1]


// Step 7: Display object information
.((`real;c1[`Real]);(`imag;c1[`Imaginary]);(`magnitude;magnitude);(`conj_real;conj_result[`Real]);(`conj_imag;conj_result[`Imaginary]);(`instance_more_than_5_methods;5<#!c1);(`type_more_than_20_methods;20<#!complex))